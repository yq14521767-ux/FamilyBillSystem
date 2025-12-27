// pages/bills/bills.js
const app = getApp();
const { debounce, showLoading, hideLoading, showToast } = require('../../utils/common');

Page({
  data: {
    // 筛选条件
    selectedYear: new Date().getFullYear(),
    selectedMonth: new Date().getMonth() + 1,
    timeText: '',
    selectedCategoryId: null,
    selectedCategoryName: '全部分类',
    selectedType: null,
    selectedTypeName: '全部',
    
    // 显示数据
    billGroups: [],
    monthStats: {
      income: '0.00',
      expense: '0.00',
      balance: '0.00'
    },
    categories: [],
    
    // 家庭选择
    families: [],
    selectedFamilyId: null,
    selectedFamilyName: '',
    selectedFamilyIndex: 0,
    
    // 分页
    currentPage: 1,
    pageSize: 20,
    hasMore: true,
    loading: false,
    
    // 弹窗状态
    showDatePicker: false,
    showCategoryFilter: false,
    showTypeFilter: false,
    showBillActions: false,
    
    // 日期选择器
    years: [],
    months: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
    datePickerValue: [0, 0],
    
    // 当前操作的账单
    currentBill: null,
    
    // 缓存
    _cache: new Map(),
    _cacheExpiry: 5 * 60 * 1000 // 5分钟缓存
  },

  onLoad() {
    this.initDatePicker();
    this.updateTimeText();
    this.loadCategories();
    this.loadFamilies();
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }
    
    this.loadFamilies();
    this.refreshData();
  },

  // 初始化日期选择器
  initDatePicker() {
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let i = currentYear - 5; i <= currentYear + 1; i++) {
      years.push(i);
    }
    
    const yearIndex = years.indexOf(this.data.selectedYear);
    const monthIndex = this.data.selectedMonth - 1;
    
    this.setData({
      years,
      datePickerValue: [yearIndex, monthIndex]
    });
  },

  // 加载分类数据
  async loadCategories() {
    try {
      const res = await app.request({
        url: '/categories'
      });
      
      // categories 中同时包含系统分类和自定义分类，
      // 账单筛选弹窗会直接遍历该列表，确保自定义分类也能参与筛选
      this.setData({
        categories: res.data || []
      });
    } catch (error) {
      console.error('加载分类失败:', error);
    }
  },

  // 加载家庭列表，用于家庭筛选
  async loadFamilies() {
    try {
      const res = await app.request({
        url: '/families'
      });

      const families = res.data || [];
      let { selectedFamilyId, selectedFamilyName, selectedFamilyIndex } = this.data;

      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');

      if (families.length > 0) {
        let index = -1;
        if (currentFamily && currentFamily.id) {
          index = families.findIndex(f => f.id === currentFamily.id);
        }
        if (index === -1) {
          index = 0;
        }

        selectedFamilyId = families[index].id;
        selectedFamilyName = families[index].name;
        selectedFamilyIndex = index;
      } else {
        selectedFamilyId = null;
        selectedFamilyName = '未选择家庭';
        selectedFamilyIndex = 0;
      }

      this.setData({
        families,
        selectedFamilyId,
        selectedFamilyName,
        selectedFamilyIndex
      });
    } catch (error) {
      console.error('加载家庭列表失败:', error);
    }
  },

  // 家庭选择变更
  onFamilyChange(e) {
    const index = e.detail.value;
    const { families } = this.data;

    if (!families || families.length === 0) return;

    const family = families[index];
    this.setData({
      selectedFamilyId: family.id,
      selectedFamilyName: family.name,
      selectedFamilyIndex: index
    });

    this.refreshData();
  },

  // 刷新数据
  refreshData() {
    this.setData({
      billGroups: [],
      currentPage: 1,
      hasMore: true
    });
    
    this.loadMonthStats();
    this.loadBills();
  },

  // 加载月度统计
  async loadMonthStats() {
    try {
      const { selectedYear, selectedMonth, selectedFamilyId } = this.data;
      let familyId = selectedFamilyId;

      // 如果未选择家庭，则回退到当前家庭
      if (!familyId) {
        const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
        if (currentFamily && currentFamily.id) {
          familyId = currentFamily.id;
        }
      }

      let url = `/bills/statistics/monthly?year=${selectedYear}&month=${selectedMonth}`;
      if (familyId) {
        url += `&familyId=${familyId}`;
      }

      const res = await app.request({ url });

      this.setData({
        monthStats: {
          income: app.formatAmount(res.income || 0),
          expense: app.formatAmount(res.expense || 0),
          balance: app.formatAmount((res.income || 0) - (res.expense || 0))
        }
      });
    } catch (error) {
      console.error('加载月度统计失败:', error);
    }
  },

  // 加载账单列表
  async loadBills(loadMore = false) {
    if (this.data.loading) return;
    
    this.setData({ loading: true });

    try {
      const { 
        selectedYear, selectedMonth, selectedCategoryId, selectedType,
        currentPage, pageSize, selectedFamilyId
      } = this.data;

      let familyId = selectedFamilyId;

      // 如果未选择家庭，则回退到当前家庭
      if (!familyId) {
        const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
        if (currentFamily && currentFamily.id) {
          familyId = currentFamily.id;
        }
      }

      const params = {
        page: loadMore ? currentPage + 1 : 1,
        pageSize,
        year: selectedYear,
        month: selectedMonth,
        sortBy: 'BillDate',
        sortDescending: true
      };

      if (familyId) {
        params.familyId = familyId;
      }

      if (selectedCategoryId) {
        params.categoryId = selectedCategoryId;
      }

      if (selectedType) {
        params.categoryType = selectedType;
      }

      const queryString = Object.keys(params)
        .map(key => `${key}=${encodeURIComponent(params[key])}`)
        .join('&');

      const res = await app.request({
        url: `/bills?${queryString}`
      });

      const bills = (res.data || []).map(bill => ({
        ...bill,
        amount: app.formatAmount(bill.amount)
      }));

      // 按日期分组
      const groups = this.groupBillsByDate(bills);
      
      if (loadMore) {
        const existingGroups = this.data.billGroups;
        const mergedGroups = this.mergeGroups(existingGroups, groups);
        this.setData({
          billGroups: mergedGroups,
          currentPage: currentPage + 1,
          hasMore: bills.length === pageSize
        });
      } else {
        this.setData({
          billGroups: groups,
          currentPage: 1,
          hasMore: bills.length === pageSize
        });
      }

    } catch (error) {
      console.error('加载账单失败:', error);
      app.showToast('加载失败，请重试');
    } finally {
      this.setData({ loading: false });
    }
  },

  // 按日期分组账单
  groupBillsByDate(bills) {
    const groups = {};
    
    bills.forEach(bill => {
      const date = app.formatDate(bill.billDate, 'MM-DD');
      const weekday = this.getWeekday(bill.billDate);
      const displayDate = `${date} ${weekday}`;
      
      if (!groups[displayDate]) {
        groups[displayDate] = {
          date: displayDate,
          bills: [],
          totalAmount: 0
        };
      }
      
      groups[displayDate].bills.push(bill);
      
      // 计算当日总金额（收入为正，支出为负）
      const amount = parseFloat(bill.amount);
      if (bill.categoryType === 1) { // 收入
        groups[displayDate].totalAmount += amount;
      } else { // 支出
        groups[displayDate].totalAmount -= amount;
      }
    });

    // 转换为数组并格式化金额
    return Object.values(groups).map(group => ({
      ...group,
      totalAmount: app.formatAmount(group.totalAmount)
    }));
  },

  // 获取星期几
  getWeekday(dateString) {
    const date = new Date(dateString);
    const weekdays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'];
    return weekdays[date.getDay()];
  },

  // 合并分组（用于加载更多）
  mergeGroups(existingGroups, newGroups) {
    const merged = [...existingGroups];
    
    newGroups.forEach(newGroup => {
      const existingIndex = merged.findIndex(g => g.date === newGroup.date);
      if (existingIndex >= 0) {
        // 合并同一天的账单
        merged[existingIndex].bills.push(...newGroup.bills);
        // 重新计算总金额
        const totalAmount = merged[existingIndex].bills.reduce((sum, bill) => {
          const amount = parseFloat(bill.amount);
          return sum + (bill.categoryType === 1 ? amount : -amount);
        }, 0);
        merged[existingIndex].totalAmount = app.formatAmount(totalAmount);
      } else {
        merged.push(newGroup);
      }
    });
    
    return merged;
  },

  // 更新时间显示文本（账单页）
  updateTimeText() {
    const { selectedYear, selectedMonth } = this.data;
    const now = new Date();
    const currentYear = now.getFullYear();
    const currentMonth = now.getMonth() + 1;

    let timeText = '';
    if (selectedYear === currentYear && selectedMonth === currentMonth) {
      timeText = '本月';
    } else {
      timeText = `${selectedYear}年${selectedMonth}月`;
    }

    this.setData({ timeText });
  },

  // 显示日期选择器
  showDatePicker() {
    this.setData({ showDatePicker: true });
  },

  // 隐藏日期选择器
  hideDatePicker() {
    this.setData({ showDatePicker: false });
  },

  // 日期选择器变化
  onDatePickerChange(e) {
    this.setData({
      datePickerValue: e.detail.value
    });
  },

  // 确认日期选择
  confirmDatePicker() {
    const { years, months, datePickerValue } = this.data;
    const selectedYear = years[datePickerValue[0]];
    const selectedMonth = months[datePickerValue[1]];
    
    this.setData({
      selectedYear,
      selectedMonth,
      showDatePicker: false
    }, () => {
      this.updateTimeText();
    });

    this.refreshData();
  },

  // 显示分类筛选
  showCategoryFilter() {
    this.setData({ showCategoryFilter: true });
  },

  // 隐藏分类筛选
  hideCategoryFilter() {
    this.setData({ showCategoryFilter: false });
  },

  // 选择分类筛选
  selectCategoryFilter(e) {
    const id = e.currentTarget.dataset.id;
    const name = e.currentTarget.dataset.name || '全部分类';
    
    this.setData({
      selectedCategoryId: id,
      selectedCategoryName: name,
      showCategoryFilter: false
    });
    
    this.refreshData();
  },

  // 显示类型筛选
  showTypeFilter() {
    this.setData({ showTypeFilter: true });
  },

  // 隐藏类型筛选
  hideTypeFilter() {
    this.setData({ showTypeFilter: false });
  },

  // 选择类型筛选
  selectTypeFilter(e) {
    const type = e.currentTarget.dataset.type;
    let typeName = '全部';
    
    if (type === 1) {
      typeName = '收入';
    } else if (type === 2) {
      typeName = '支出';
    }
    
    this.setData({
      selectedType: type,
      selectedTypeName: typeName,
      showTypeFilter: false
    });
    
    this.refreshData();
  },

  // 加载更多
  loadMore() {
    this.loadBills(true);
  },

  // 查看账单详情
  viewBillDetail(e) {
    const bill = e.currentTarget.dataset.bill;
    wx.navigateTo({
      url: `/pages/bill-detail/bill-detail?id=${bill.id}`
    });
  },

  // 显示账单操作
  showBillActions(e) {
    const bill = e.currentTarget.dataset.bill;
    this.setData({
      currentBill: bill,
      showBillActions: true
    });
  },

  // 隐藏账单操作
  hideBillActions() {
    this.setData({
      showBillActions: false,
      currentBill: null
    });
  },

  // 编辑账单
  editBill() {
    const { currentBill } = this.data;
    this.hideBillActions();
    
    wx.navigateTo({
      url: `/pages/edit-bill/edit-bill?id=${currentBill.id}`
    });
  },

  // 删除账单
  async deleteBill() {
    const { currentBill } = this.data;
    
    const confirmed = await app.showModal('确认删除', '确定要删除这条账单记录吗？');
    if (!confirmed) return;
    
    this.hideBillActions();
    
    try {
      app.showLoading('删除中...');
      
      await app.request({
        url: `/bills/${currentBill.id}`,
        method: 'DELETE'
      });
      
      app.showToast('删除成功', 'success');
      this.refreshData();
    } catch (error) {
      console.error('删除账单失败:', error);
      app.showToast('删除失败，请重试');
    } finally {
      app.hideLoading();
    }
  },

  // 添加账单
  addBill() {
    wx.navigateTo({
      url: '/pages/add-bill/add-bill'
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.refreshData();
    wx.stopPullDownRefresh();
  },

  // 触底加载更多
  onReachBottom() {
    if (this.data.hasMore && !this.data.loading) {
      this.loadMore();
    }
  },

  // 缓存相关方法
  getCacheKey(params) {
    return JSON.stringify(params);
  },

  getFromCache(key) {
    const cached = this.data._cache.get(key);
    if (cached && Date.now() - cached.timestamp < this.data._cacheExpiry) {
      return cached.data;
    }
    this.data._cache.delete(key);
    return null;
  },

  setCache(key, data) {
    this.data._cache.set(key, {
      data,
      timestamp: Date.now()
    });
  },

  clearCache() {
    this.data._cache.clear();
  },

  // 防抖的数据刷新
  debouncedRefresh: null,

  onReady() {
    // 初始化防抖函数
    this.debouncedRefresh = debounce(() => {
      this.refreshData();
    }, 300);
  }
});
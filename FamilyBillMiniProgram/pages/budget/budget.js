// pages/budget/budget.js
const app = getApp();

Page({
  data: {
    // 时间选择
    selectedYear: new Date().getFullYear(),
    selectedMonth: new Date().getMonth() + 1,
    timeText: '',
    // 预算周期：monthly | quarterly | yearly
    selectedPeriod: 'monthly',
    
    // 预算数据
    budgets: [],
    totalBudget: '0.00',
    totalUsed: '0.00',
    totalRemaining: '0.00',
    
    // 分类数据
    expenseCategories: [],
    selectedCategory: null,

    // 家庭选择
    families: [],
    selectedFamilyId: null,
    selectedFamilyName: '',
    selectedFamilyIndex: 0,
    
    // 弹窗状态
    showTimePicker: false,
    showBudgetModal: false,
    showCategoryPicker: false,
    
    // 表单数据
    budgetForm: {
      id: null,
      categoryId: null,
      amount: '',
      description: ''
    },
    isEditMode: false,
    
    // 时间选择器
    years: [],
    months: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
    quarters: [1, 4, 7, 10], // 每个季度的起始月份
    timePickerValue: [0, 0],
    
    // 加载状态
    saveLoading: false
  },

  onLoad() {
    this.initTimePicker();
    this.updateTimeText();
    this.loadExpenseCategories();
    this.loadFamilies();
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }
    
    this.loadBudgets();
  },

  // 初始化时间选择器
  initTimePicker() {
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let i = currentYear - 2; i <= currentYear + 2; i++) {
      years.push(i);
    }
    
    const { selectedYear, selectedMonth, selectedPeriod, months, quarters } = this.data;

    let yearIndex = years.indexOf(selectedYear);
    if (yearIndex < 0) {
      const currentIndex = years.indexOf(currentYear);
      yearIndex = currentIndex >= 0 ? currentIndex : 0;
    }

    let secondIndex = 0;
    if (selectedPeriod === 'monthly') {
      const idx = months.indexOf(selectedMonth);
      secondIndex = idx >= 0 ? idx : 0;
    } else if (selectedPeriod === 'quarterly') {
      const idx = quarters.indexOf(selectedMonth);
      secondIndex = idx >= 0 ? idx : 0;
    }

    this.setData({
      years,
      timePickerValue: [yearIndex, secondIndex]
    });
  },

  // 加载支出分类
  async loadExpenseCategories() {
    try {
      const res = await app.request({
        url: '/categories?type=2' // 支出分类
      });
      
      this.setData({
        expenseCategories: res.data || []
      });
    } catch (error) {
      console.error('加载分类失败:', error);
    }
  },

  // 加载预算数据
  async loadBudgets() {
    try {
      const { selectedYear, selectedMonth, selectedFamilyId, selectedPeriod } = this.data;
      let familyId = selectedFamilyId;

      // 如果未通过下拉选择家庭，则回退到当前家庭
      if (!familyId) {
        const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
        if (currentFamily && currentFamily.id) {
          familyId = currentFamily.id;
        }
      }
      
      let url = `/budgets?year=${selectedYear}&month=${selectedMonth}&period=${selectedPeriod}`;
      if (familyId) {
        url += `&familyId=${familyId}`;
      }
      
      const res = await app.request({ url });
      
      const budgets = (res.data || []).map(budget => {
        const remaining = budget.amount - budget.usedAmount;
        const usagePercentage = budget.amount > 0 ? Math.round((budget.usedAmount / budget.amount) * 100) : 0;
        
        return {
          ...budget,
          amount: app.formatAmount(budget.amount),
          usedAmount: app.formatAmount(budget.usedAmount),
          remaining: app.formatAmount(remaining),
          usagePercentage,
          isOverBudget: budget.usedAmount > budget.amount
        };
      });
      
      // 计算总计
      const totalBudget = budgets.reduce((sum, item) => sum + parseFloat(item.amount), 0);
      const totalUsed = budgets.reduce((sum, item) => sum + parseFloat(item.usedAmount), 0);
      const totalRemaining = totalBudget - totalUsed;
      
      this.setData({
        budgets,
        totalBudget: app.formatAmount(totalBudget),
        totalUsed: app.formatAmount(totalUsed),
        totalRemaining: app.formatAmount(totalRemaining)
      });
    } catch (error) {
      console.error('加载预算失败:', error);
      app.showToast('加载预算失败');
    }
  },

  // 计算时间显示文本
  updateTimeText() {
    const { selectedYear, selectedMonth, selectedPeriod } = this.data;
    const now = new Date();
    const currentYear = now.getFullYear();
    const currentMonth = now.getMonth() + 1;

    let timeText = '';
    if (selectedPeriod === 'monthly') {
      if (selectedYear === currentYear && selectedMonth === currentMonth) {
        timeText = '本月预算';
      } else {
        timeText = `${selectedYear}年${selectedMonth}月预算`;
      }
    } else if (selectedPeriod === 'quarterly') {
      const currentQuarter = Math.floor((currentMonth - 1) / 3) + 1;
      const selectedQuarter = Math.floor((selectedMonth - 1) / 3) + 1;
      if (selectedYear === currentYear && selectedQuarter === currentQuarter) {
        timeText = '本季度预算';
      } else {
        timeText = `${selectedYear}年第${selectedQuarter}季度预算`;
      }
    } else if (selectedPeriod === 'yearly') {
      if (selectedYear === currentYear) {
        timeText = '本年度预算';
      } else {
        timeText = `${selectedYear}年度预算`;
      }
    }

    this.setData({ timeText });
  },

  // 显示时间选择器
  showTimePicker() {
    this.setData({ showTimePicker: true });
  },

  // 隐藏时间选择器
  hideTimePicker() {
    this.setData({ showTimePicker: false });
  },

  // 时间选择器变化
  onTimePickerChange(e) {
    this.setData({
      timePickerValue: e.detail.value
    });
  },

  // 确认时间选择
  confirmTimePicker() {
    const { years, months, quarters, timePickerValue, selectedPeriod } = this.data;

    const yearIndex = (timePickerValue && timePickerValue.length > 0) ? timePickerValue[0] : 0;
    const selectedYear = years[yearIndex] || years[0];

    let selectedMonth = this.data.selectedMonth;

    if (selectedPeriod === 'monthly') {
      const monthIndex = (timePickerValue && timePickerValue.length > 1) ? timePickerValue[1] : 0;
      selectedMonth = months[monthIndex] || months[0];
    } else if (selectedPeriod === 'quarterly') {
      const quarterIndex = (timePickerValue && timePickerValue.length > 1) ? timePickerValue[1] : 0;
      // 这里 quarters 存的是每个季度的起始月份：1、4、7、10
      selectedMonth = quarters[quarterIndex] || quarters[0];
    } else if (selectedPeriod === 'yearly') {
      // 年度预算只按年份统计，月份字段对后端影响不大，这里统一设置为 1
      selectedMonth = 1;
    }

    this.setData({
      selectedYear,
      selectedMonth,
      showTimePicker: false
    });

    this.updateTimeText();
    this.loadBudgets();
  },

  // 切换预算周期
  onPeriodTabTap(e) {
    const period = e.currentTarget.dataset.period;
    if (!period || period === this.data.selectedPeriod) return;

    const { years, selectedYear, selectedMonth, months, quarters } = this.data;

    let yearIndex = years.indexOf(selectedYear);
    if (yearIndex < 0) {
      yearIndex = 0;
    }

    let secondIndex = 0;
    if (period === 'monthly') {
      const idx = months.indexOf(selectedMonth);
      secondIndex = idx >= 0 ? idx : 0;
    } else if (period === 'quarterly') {
      const idx = quarters.indexOf(selectedMonth);
      secondIndex = idx >= 0 ? idx : 0;
    } else if (period === 'yearly') {
      secondIndex = 0;
    }

    this.setData({
      selectedPeriod: period,
      timePickerValue: [yearIndex, secondIndex]
    });

    this.updateTimeText();
    this.loadBudgets();
  },

  // 加载家庭列表，用于家庭筛选
  async loadFamilies() {
    try {
      const res = await app.request({
        url: '/families'
      });

      const families = res.data || [];
      let selectedFamilyId = this.data.selectedFamilyId;
      let selectedFamilyName = this.data.selectedFamilyName;
      let selectedFamilyIndex = this.data.selectedFamilyIndex;

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

    this.loadBudgets();
  },

  // 显示添加预算弹窗
  showAddBudgetModal() {
    this.setData({
      showBudgetModal: true,
      isEditMode: false,
      budgetForm: {
        id: null,
        categoryId: null,
        amount: '',
        description: ''
      },
      selectedCategory: null
    });
  },

  // 编辑预算
  editBudget(e) {
    const budget = e.currentTarget.dataset.budget;
    const category = this.data.expenseCategories.find(cat => cat.id === budget.categoryId);
    
    this.setData({
      showBudgetModal: true,
      isEditMode: true,
      budgetForm: {
        id: budget.id,
        categoryId: budget.categoryId,
        amount: parseFloat(budget.amount).toString(),
        description: budget.description || ''
      },
      selectedCategory: category
    });
  },

  // 隐藏预算弹窗
  hideBudgetModal() {
    this.setData({
      showBudgetModal: false
    });
  },

  // 显示分类选择器
  showCategoryPicker() {
    this.setData({
      showCategoryPicker: true
    });
  },

  // 隐藏分类选择器
  hideCategoryPicker() {
    this.setData({
      showCategoryPicker: false
    });
  },

  // 选择分类
  selectCategory(e) {
    const category = e.currentTarget.dataset.category;
    this.setData({
      'budgetForm.categoryId': category.id,
      selectedCategory: category
    });
  },

  // 预算表单输入
  onBudgetAmountInput(e) {
    this.setData({
      'budgetForm.amount': e.detail.value
    });
  },

  onBudgetDescriptionInput(e) {
    this.setData({
      'budgetForm.description': e.detail.value
    });
  },

  // 保存预算
  async saveBudget() {
    const { budgetForm, selectedYear, selectedMonth, selectedPeriod, isEditMode, selectedFamilyId } = this.data;
    
    if (!budgetForm.categoryId) {
      app.showToast('请选择分类');
      return;
    }
    
    if (!budgetForm.amount || parseFloat(budgetForm.amount) <= 0) {
      app.showToast('请输入有效的预算金额');
      return;
    }
    
    // 确保有家庭ID：优先使用下拉选择的家庭，没有则回退到当前家庭
    let familyId = selectedFamilyId;
    if (!familyId) {
      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
      if (!currentFamily || !currentFamily.id) {
        app.showToast('请先在家庭页选择一个家庭');
        return;
      }
      familyId = currentFamily.id;
    }
    
    this.setData({ saveLoading: true });
    
    try {
      const budgetData = {
        categoryId: budgetForm.categoryId,
        amount: parseFloat(budgetForm.amount),
        year: selectedYear,
        month: selectedMonth,
        period: selectedPeriod,
        description: budgetForm.description.trim() || null,
        familyId: familyId
      };
      
      if (isEditMode) {
        await app.request({
          url: `/budgets/${budgetForm.id}`,
          method: 'PUT',
          data: budgetData
        });
        app.showToast('预算更新成功', 'success');
      } else {
        await app.request({
          url: '/budgets',
          method: 'POST',
          data: budgetData
        });
        app.showToast('预算添加成功', 'success');
      }
      
      this.hideBudgetModal();
      this.loadBudgets();
    } catch (error) {
      console.error('保存预算失败:', error);
      app.showToast(error.message || '保存失败，请重试');
    } finally {
      this.setData({ saveLoading: false });
    }
  },

  // 删除预算
  async deleteBudget(e) {
    const id = e.currentTarget.dataset.id;
    
    const confirmed = await app.showModal('确认删除', '确定要删除这个预算吗？');
    if (!confirmed) return;
    
    try {
      app.showLoading('删除中...');
      
      await app.request({
        url: `/budgets/${id}`,
        method: 'DELETE'
      });
      
      app.showToast('预算删除成功', 'success');
      this.loadBudgets();
    } catch (error) {
      console.error('删除预算失败:', error);
      app.showToast('删除失败，请重试');
    } finally {
      app.hideLoading();
    }
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadBudgets().finally(() => {
      wx.stopPullDownRefresh();
    });
  }
});
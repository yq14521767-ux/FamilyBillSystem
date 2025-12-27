// pages/add-bill/add-bill.js
const app = getApp();

Page({
  data: {
    billType: 'expense', // expense | income
    amount: '',
    selectedCategory: null,
    billDate: '',
    description: '',
    remark: '',
    loading: false,
    amountFocus: true,
    canSubmit: false,
    
    // 分类相关
    showCategoryModal: false,
    categoryTab: 'system', // system | custom
    categories: [],
    filteredCategories: [],
    
    // 添加分类
    showAddCategoryForm: false,
    newCategory: {
      name: '',
      color: '#4CAF50'
    },
    colorOptions: [
      '#4CAF50', '#2196F3', '#FF9800', '#f44336', 
      '#9C27B0', '#607D8B', '#795548', '#E91E63',
      '#3F51B5', '#009688', '#8BC34A', '#CDDC39',
      '#FFC107', '#FF5722', '#9E9E9E', '#673AB7'
    ],

    // 支付方式
    paymentMethodOptions: ['微信', '支付宝', '现金', '信用卡', '其他'],
    paymentMethodIndex: 0,
    paymentMethod: '',

    // 家庭选择
    families: [],
    selectedFamilyId: null,
    selectedFamilyName: '',
    selectedFamilyIndex: 0
  },

  onLoad(options) {
    // 设置默认日期为今天
    const today = new Date();
    const billDate = app.formatDate(today, 'YYYY-MM-DD');
    
    // 从参数获取类型
    const billType = options.type || 'expense';
    
    this.setData({
      billDate,
      billType
    });

    this.loadCategories();
  },

  onShow() {
    // 检查登录状态
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }

    // 确保全局 currentFamily 与本地缓存同步，避免家庭页未打开时使用旧家庭
    const cachedFamily = wx.getStorageSync('currentFamily');
    if (cachedFamily && cachedFamily.id) {
      app.globalData.currentFamily = cachedFamily;
    }

    // 加载家庭列表，默认选中当前家庭
    this.loadFamilies();
  },

  // 加载分类数据
  async loadCategories() {
    try {
      const res = await app.request({
        url: '/categories'
      });

      this.setData({
        categories: res.data || []
      });

      this.filterCategories();
    } catch (error) {
      console.error('加载分类失败:', error);
      app.showToast('加载分类失败');
    }
  },

  // 加载家庭列表，用于选择账单所属家庭
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

  // 筛选分类
  filterCategories() {
    const { categories, billType, categoryTab } = this.data;
    const targetType = billType === 'expense' ? 2 : 1; // CategoryType.Expense = 2, CategoryType.Income = 1

    let filtered = categories.filter(cat => cat.type === targetType);
    
    if (categoryTab === 'system') {
      filtered = filtered.filter(cat => cat.isSystem);
    } else {
      filtered = filtered.filter(cat => !cat.isSystem);
    }

    this.setData({
      filteredCategories: filtered
    });
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
  },

  // 支付方式选择
  onPaymentMethodChange(e) {
    const index = e.detail.value;
    const { paymentMethodOptions } = this.data;
    if (!paymentMethodOptions || paymentMethodOptions.length === 0) return;

    const method = paymentMethodOptions[index];
    this.setData({
      paymentMethodIndex: index,
      paymentMethod: method
    });
  },

  // 切换收支类型
  switchType(e) {
    const type = e.currentTarget.dataset.type;
    this.setData({
      billType: type,
      selectedCategory: null
    });
    this.filterCategories();
  },

  // 金额输入
  onAmountInput(e) {
    let value = e.detail.value;
    
    // 限制小数点后两位
    if (value.includes('.')) {
      const parts = value.split('.');
      if (parts[1] && parts[1].length > 2) {
        value = parts[0] + '.' + parts[1].substring(0, 2);
      }
    }

    this.setData({
      amount: value
    });
    this.updateCanSubmit();
  },

  // 显示分类选择器
  showCategoryPicker() {
    this.setData({
      showCategoryModal: true
    });
  },

  // 隐藏分类选择器
  hideCategoryPicker() {
    this.setData({
      showCategoryModal: false
    });
  },

  // 切换分类标签
  switchCategoryTab(e) {
    const tab = e.currentTarget.dataset.tab;
    this.setData({
      categoryTab: tab
    });
    this.filterCategories();
  },

  // 选择分类
  selectCategory(e) {
    const category = e.currentTarget.dataset.category;
    this.setData({
      selectedCategory: category,
      showCategoryModal: false
    });
    this.updateCanSubmit();
  },

  // 显示添加分类表单
  showAddCategoryForm() {
    this.setData({
      showAddCategoryForm: true,
      newCategory: {
        name: '',
        color: '#4CAF50'
      }
    });
  },

  // 隐藏添加分类表单
  hideAddCategoryForm() {
    this.setData({
      showAddCategoryForm: false
    });
  },

  // 新分类名称输入
  onNewCategoryNameInput(e) {
    this.setData({
      'newCategory.name': e.detail.value
    });
  },

  // 选择颜色
  selectColor(e) {
    const color = e.currentTarget.dataset.color;
    this.setData({
      'newCategory.color': color
    });
  },

  // 添加自定义分类
  async addCustomCategory() {
    const { newCategory, billType } = this.data;
    
    if (!newCategory.name.trim()) {
      app.showToast('请输入分类名称');
      return;
    }

    try {
      app.showLoading('添加中...');
      
      const categoryData = {
        name: newCategory.name.trim(),
        type: billType === 'expense' ? 'expense' : 'income',
        color: newCategory.color,
        icon: newCategory.name.charAt(0)
      };

      const res = await app.request({
        url: '/categories',
        method: 'POST',
        data: categoryData
      });

      // 添加到分类列表
      const categories = [...this.data.categories, res];
      this.setData({
        categories,
        showAddCategoryForm: false
      });

      this.filterCategories();
      app.showToast('分类添加成功', 'success');
    } catch (error) {
      console.error('添加分类失败:', error);
      app.showToast('添加分类失败');
    } finally {
      app.hideLoading();
    }
  },

  // 日期选择
  onDateChange(e) {
    this.setData({
      billDate: e.detail.value
    });
  },

  // 备注输入
  onDescriptionInput(e) {
    this.setData({
      description: e.detail.value
    });
  },

  // 详细备注输入
  onRemarkInput(e) {
    this.setData({
      remark: e.detail.value
    });
  },

  // 检查是否可以提交
  updateCanSubmit() {
    const { amount, selectedCategory } = this.data;
    const canSubmit = !!amount && parseFloat(amount) > 0 && !!selectedCategory;
    this.setData({ canSubmit });
  },

  // 提交账单
  async onSubmit() {
    const { amount, selectedCategory, billDate, description, remark, billType, selectedFamilyId, paymentMethod } = this.data;

    // 验证数据
    if (!amount || parseFloat(amount) <= 0) {
      app.showToast('请输入有效金额');
      return;
    }

    if (!selectedCategory) {
      app.showToast('请选择分类');
      return;
    }

    // 确保选择了家庭，用于绑定账单归属
    if (!selectedFamilyId) {
      app.showToast('请选择家庭');
      return;
    }

    this.setData({ loading: true });

    try {
      const billData = {
        familyId: selectedFamilyId,
        type: billType,
        categoryId: selectedCategory.id,
        amount: parseFloat(amount),
        billDate: billDate + 'T00:00:00',
        description: description.trim() || null,
        remark: remark.trim() || null, 
        paymentMethod: paymentMethod ? paymentMethod.trim() : null
      };

      await app.request({
        url: '/bills',
        method: 'POST',
        data: billData
      });

      app.showToast('账单保存成功', 'success');
      
      // 返回上一页或首页
      setTimeout(() => {
        const pages = getCurrentPages();
        if (pages.length > 1) {
          wx.navigateBack();
        } else {
          wx.switchTab({
            url: '/pages/index/index'
          });
        }
      }, 1000);

    } catch (error) {
      console.error('保存账单失败:', error);
      app.showToast(error.message || '保存失败，请重试');
    } finally {
      this.setData({ loading: false });
    }
  }
});
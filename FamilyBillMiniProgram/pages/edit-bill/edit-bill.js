// pages/edit-bill/edit-bill.js
const app = getApp();

Page({
  data: {
    billId: null,
    billType: 'expense',
    amount: '',
    billDate: '',
    description: '',
    remark:'',
    familyName: '',
    loading: false,
    submitting: false,
    canSubmit: false,
    paymentMethodOptions: ['微信', '支付宝', '现金', '信用卡', '其他'],
    paymentMethodIndex: 0,
    paymentMethod: '',
    categories: [],
    selectableCategories: [],
    selectedCategoryIndex: 0,
    selectedCategoryId: null,
    selectedCategoryName: ''
  },

  onLoad(options) {
    const billId = options && options.id ? parseInt(options.id, 10) : null;
    if (!billId) {
      app.showToast('无效的账单ID');
      setTimeout(() => wx.navigateBack({}), 1500);
      return;
    }
    this.setData({ billId });
  },

  async onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({ url: '/pages/login/login' });
      return;
    }

    try {
      await this.loadBillBase();
      await this.loadCategories();
    } catch (error) {
      console.error('初始化编辑账单页面失败:', error);
      app.showToast(error.message || '加载账单信息失败');
    }
  },

  async loadBillBase() {
    if (!this.data.billId) return;
    this.setData({ loading: true });
    try {
      const res = await app.request({ url: `/bills/${this.data.billId}` });
      const billType = res.type === 'income' ? 'income' : 'expense';
      const billDate = app.formatDate(res.billDate, 'YYYY-MM-DD');
      const paymentMethod = res.paymentMethod || '';
      let paymentMethodIndex = 0;
      const { paymentMethodOptions } = this.data;
      if (paymentMethod && Array.isArray(paymentMethodOptions) && paymentMethodOptions.length > 0) {
        const idx = paymentMethodOptions.findIndex(m => m === paymentMethod);
        if (idx >= 0) {
          paymentMethodIndex = idx;
        }
      }

      this.setData({
        billType,
        amount: String(res.amount || ''),
        billDate,
        description: res.description || '',
        remark: res.remark || '',
        familyName: res.familyName || '',
        paymentMethod,
        paymentMethodIndex,
        selectedCategoryId: res.categoryId || null
      });

      this.updateCanSubmit();
    } finally {
      this.setData({ loading: false });
    }
  },

  async loadCategories() {
    try {
      const res = await app.request({ url: '/categories' });
      const categories = res.data || [];
      const targetType = this.data.billType === 'income' ? 1 : 2; // 1: 收入, 2: 支出
      const selectable = categories.filter(c => c.type === targetType);

      let index = 0;
      const { selectedCategoryId } = this.data;
      if (selectedCategoryId) {
        const foundIndex = selectable.findIndex(c => c.id === selectedCategoryId);
        if (foundIndex >= 0) {
          index = foundIndex;
        }
      }

      const selected = selectable[index] || null;

      this.setData({
        categories,
        selectableCategories: selectable,
        selectedCategoryIndex: index,
        selectedCategoryId: selected ? selected.id : null,
        selectedCategoryName: selected ? selected.name : ''
      });

      this.updateCanSubmit();
    } catch (error) {
      console.error('加载分类失败:', error);
      app.showToast('加载分类失败');
    }
  },

  // 金额输入
  onAmountInput(e) {
    let value = e.detail.value;
    if (value.includes('.')) {
      const parts = value.split('.');
      if (parts[1] && parts[1].length > 2) {
        value = parts[0] + '.' + parts[1].substring(0, 2);
      }
    }

    this.setData({ amount: value });
    this.updateCanSubmit();
  },

  // 分类选择
  onCategoryChange(e) {
    const index = e.detail.value;
    const { selectableCategories } = this.data;
    if (!selectableCategories || selectableCategories.length === 0) return;

    const selected = selectableCategories[index];
    this.setData({
      selectedCategoryIndex: index,
      selectedCategoryId: selected.id,
      selectedCategoryName: selected.name
    });
    this.updateCanSubmit();
  },

  // 日期选择
  onDateChange(e) {
    this.setData({ billDate: e.detail.value });
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

  // 备注输入
  onDescriptionInput(e) {
    this.setData({ description: e.detail.value });
  },
  // 详细说明输入
  onRemarkInput(e) {
    this.setData({ remark: e.detail.value });
  },

  // 检查是否可以提交
  updateCanSubmit() {
    const { amount, selectedCategoryId } = this.data;
    const canSubmit = !!amount && parseFloat(amount) > 0 && !!selectedCategoryId;
    this.setData({ canSubmit });
  },

  // 提交更新
  async onSubmit() {
    const { billId, amount, billDate, description,remark, selectedCategoryId, canSubmit, paymentMethod } = this.data;
    if (!canSubmit) return;

    if (!billDate) {
      app.showToast('请选择日期');
      return;
    }

    this.setData({ submitting: true });
    try {
      const data = {
        categoryId: selectedCategoryId,
        amount: parseFloat(amount),
        billDate: billDate + 'T00:00:00',
        description: description.trim() || null,
        remark:remark.trim() || null,
        paymentMethod: paymentMethod ? paymentMethod.trim() : null
      };

      await app.request({
        url: `/bills/${billId}`,
        method: 'PUT',
        data
      });

      app.showToast('账单更新成功', 'success');

      setTimeout(() => {
        const pages = getCurrentPages();
        if (pages.length > 1) {
          wx.navigateBack();
        } else {
          wx.switchTab({ url: '/pages/bills/bills' });
        }
      }, 1000);
    } catch (error) {
      console.error('更新账单失败:', error);
      app.showToast(error.message || '更新失败，请重试');
    } finally {
      this.setData({ submitting: false });
    }
  }
});

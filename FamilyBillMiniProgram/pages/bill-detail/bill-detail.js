// pages/bill-detail/bill-detail.js
const app = getApp();

Page({
  data: {
    id: null,
    bill: null,
    loading: false,
  },

  onLoad(options) {
    const id = options && options.id ? parseInt(options.id, 10) : null;
    if (!id) {
      app.showToast('无效的账单ID');
      setTimeout(() => {
        wx.navigateBack({});
      }, 1500);
      return;
    }
    this.setData({ id });
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({ url: '/pages/login/login' });
      return;
    }
    this.loadBill();
  },

  async loadBill() {
    if (this.data.loading || !this.data.id) return;
    this.setData({ loading: true });

    try {
      const res = await app.request({ url: `/bills/${this.data.id}` });
      const bill = {
        ...res,
        avatarDisplayUrl: app.getAvatarProxyUrl(res.avatarUrl),
        amountText: app.formatAmount(res.amount || 0),
        billDateText: app.formatDate(res.upDatedAt, 'YYYY-MM-DD HH:mm'),
        typeText: res.type === 'income' ? '收入' : '支出',
        amountSign: res.type === 'income' ? '+' : '-',
        isIncome: res.type === 'income',
        remarkText: res.remark || '无'
      };
      this.setData({ bill });
    } catch (error) {
      console.error('加载账单详情失败:', error);
      app.showToast(error.message || '加载账单详情失败');
      setTimeout(() => {
        wx.navigateBack({});
      }, 1500);
    } finally {
      this.setData({ loading: false });
    }
  },

  // 跳转到编辑页
  goToEdit() {
    if (!this.data.id) return;
    wx.navigateTo({
      url: `/pages/edit-bill/edit-bill?id=${this.data.id}`
    });
  },

  // 删除账单
  async deleteBill() {
    const { bill } = this.data;
    
    const confirmed = await app.showModal('确认删除', '确定要删除这条账单记录吗？');
    if (!confirmed) return;
    
    // this.hideBillActions();
    
    try {
      app.showLoading('删除中...');
      
      await app.request({
        url: `/bills/${bill.id}`,
        method: 'DELETE'
      });
      
      app.showToast('删除成功', 'success');
      setTimeout(() => {
        wx.reLaunch({
          url: '/pages/bills/bills' 
        });
      }, 1500);
    } catch (error) {
      console.error('删除账单失败:', error);
      app.showToast('删除失败，请重试');
    } finally {
      app.hideLoading();
    }
  }
});

// pages/index/index.js
const app = getApp();

Page({
  data: {
    userInfo: {},
    currentFamily: null,
    greeting: '',
    currentMonth: '',
    monthlyStats: {
      income: '0.00',
      expense: '0.00',
      balance: '0.00'
    },
    recentBills: [],
    budgetAlerts: [],
    unreadNotificationsCount: 0,
    showAddOptions: false,
    loading: false,
    refreshing: false
  },

  onLoad() {
    this.checkLoginStatus();
  },

  onShow() {
    if (app.globalData.token) {
      this.initPageData();
      this.loadData();
      this.loadUnreadNotifications();
    }
  },

  // 检查登录状态
  checkLoginStatus() {
    if (!app.globalData.token) {
      // 延迟检查，给app.js的validateToken一些时间
      setTimeout(() => {
        if (!app.globalData.token) {
          wx.reLaunch({
            url: '/pages/login/login'
          });
        }
      }, 1000);
      return;
    }
  },

  // 初始化页面数据
  initPageData() {
    const userInfo = app.globalData.userInfo || {};
    const currentFamily = wx.getStorageSync('currentFamily');
    
    // 设置问候语
    const hour = new Date().getHours();
    let greeting = '早上好';
    if (hour >= 12 && hour < 18) {
      greeting = '下午好';
    } else if (hour >= 18) {
      greeting = '晚上好';
    }

    // 设置当前月份
    const now = new Date();
    const currentMonth = `${now.getFullYear()}年${now.getMonth() + 1}月`;

    // 确保用户信息完整
    const rawAvatar = userInfo.avatar || userInfo.avatarUrl;
    const avatar = app.getAvatarProxyUrl(rawAvatar) || '/images/user.png';

    const completeUserInfo = {
      ...userInfo,
      nickName: userInfo.nickName || userInfo.nickname || '用户',
      avatar
    };

    this.setData({
      userInfo: completeUserInfo,
      currentFamily,
      greeting,
      currentMonth
    });
  },

  // 加载数据
  async loadData() {
    if (this.data.loading) return;
    
    this.setData({ loading: true });
    
    try {
      // 使用新的首页概览API，一次性获取所有数据
      const res = await app.request({
        url: '/dashboard/overview'
      });

      const data = res.data || {};
      
      // 设置月度统计
      const monthlyStats = data.monthlyStats || {};
      this.setData({
        monthlyStats: {
          income: app.formatAmount(monthlyStats.totalIncome || 0),
          expense: app.formatAmount(monthlyStats.totalExpense || 0),
          balance: app.formatAmount(monthlyStats.balance || 0)
        }
      });

      // 设置最近账单
      const recentBills = (data.recentBills || []).map(bill => ({
        ...bill,
        billDate: app.formatDate(bill.billDate, 'MM-DD'),
        amount: app.formatAmount(bill.amount)
      }));
      this.setData({ recentBills });

      // 设置预算提醒
      const budgetAlerts = data.budgetAlerts || [];
      this.setData({ budgetAlerts });

    } catch (error) {
      console.error('加载数据失败:', error);
      // 如果新API失败，回退到原来的方式
      try {
        await Promise.all([
          this.loadMonthlyStats(),
          this.loadRecentBills(),
          this.loadBudgetAlerts()
        ]);
      } catch (fallbackError) {
        console.error('回退加载也失败:', fallbackError);
        app.showToast('加载数据失败，请稍后重试');
      }
    } finally {
      this.setData({ loading: false });
    }
  },

  // 加载月度统计
  async loadMonthlyStats() {
    try {
      const now = new Date();
      const year = now.getFullYear();
      const month = now.getMonth() + 1;

      const res = await app.request({
        url: `/bills/statistics/monthly?year=${year}&month=${month}`
      });

      this.setData({
        monthlyStats: {
          income: app.formatAmount(res.totalIncome || 0),
          expense: app.formatAmount(res.totalExpense || 0),
          balance: app.formatAmount(res.balance || 0)
        }
      });
    } catch (error) {
      console.error('加载月度统计失败:', error);
      if (error.statusCode !== 404) {
        app.showToast('加载统计数据失败');
      }
    }
  },

  // 加载最近账单
  async loadRecentBills() {
    try {
      const res = await app.request({
        url: '/bills?page=1&pageSize=5&sortBy=BillDate&sortDescending=true'
      });

      const bills = (res.data || []).map(bill => ({
        ...bill,
        billDate: app.formatDate(bill.billDate, 'MM-DD'),
        amount: app.formatAmount(bill.amount)
      }));

      this.setData({
        recentBills: bills
      });
    } catch (error) {
      console.error('加载最近账单失败:', error);
      if (error.statusCode !== 404) {
        app.showToast('加载账单数据失败');
      }
    }
  },

  // 加载预算提醒
  async loadBudgetAlerts() {
    try {
      const now = new Date();
      const year = now.getFullYear();
      const month = now.getMonth() + 1;

      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
      let url = `/budgets/summary?year=${year}&month=${month}`;
      if (currentFamily && currentFamily.id) {
        url += `&familyId=${currentFamily.id}`;
      }

      const res = await app.request({
        url
      });

      // 使用新的API响应格式
      const alerts = (res.alerts || []).map(alert => ({
        ...alert,
        message: alert.message || '预算提醒'
      }));

      this.setData({
        budgetAlerts: alerts
      });
    } catch (error) {
      console.error('加载预算提醒失败:', error);
      if (error.statusCode !== 404) {
        app.showToast('加载预算数据失败');
      }
    }
  },

  // 加载未读通知数量
  async loadUnreadNotifications() {
    try {
      const res = await app.request({
        url: '/notifications?status=unread&page=1&pageSize=1'
      });

      const unreadCount = res.unreadCount || 0;
      this.setData({ unreadNotificationsCount: unreadCount });
    } catch (error) {
      console.error('加载未读通知数量失败:', error);
      // 不弹toast，避免打扰用户
    }
  },

  // 快速添加支出
  quickAddExpense() {
    this.hideAddOptions();
    wx.navigateTo({
      url: '/pages/add-bill/add-bill?type=expense'
    });
  },

  // 快速添加收入
  quickAddIncome() {
    this.hideAddOptions();
    wx.navigateTo({
      url: '/pages/add-bill/add-bill?type=income'
    });
  },

  // 跳转到账单页面
  goToBills() {
    wx.switchTab({
      url: '/pages/bills/bills'
    });
  },

  // 跳转到统计页面
  goToStatistics() {
    wx.switchTab({
      url: '/pages/statistics/statistics'
    });
  },

   // 跳转到预算设置页面
   goToBudget() {
     wx.navigateTo({
       url: '/pages/budget/budget'
     });
   },

   // 跳转到消息中心页面
   goToNotifications() {
     wx.navigateTo({
       url: '/pages/notifications/notifications'
     });
   },

  // 跳转到家庭页面
  goToFamily() {
    wx.switchTab({
      url: '/pages/family/family'
    });
  },

  // 查看账单详情
  viewBillDetail(e) {
    const bill = e.currentTarget.dataset.bill;
    wx.navigateTo({
      url: `/pages/bill-detail/bill-detail?id=${bill.id}`
    });
  },

  // 显示添加选项
  showAddOptions() {
    this.setData({
      showAddOptions: true
    });
  },

  // 隐藏添加选项
  hideAddOptions() {
    this.setData({
      showAddOptions: false
    });
  },

  // 下拉刷新
  async onPullDownRefresh() {
    if (this.data.refreshing) return;
    
    this.setData({ refreshing: true });
    
    try {
      // 重新初始化页面数据
      this.initPageData();
      // 重新加载数据
      await this.loadData();
    } catch (error) {
      console.error('刷新失败:', error);
      app.showToast('刷新失败，请稍后重试');
    } finally {
      this.setData({ refreshing: false });
      wx.stopPullDownRefresh();
    }
  },
  
  // 头像加载失败处理
  onAvatarLoadError() {
    this.setData({
      'userInfo.avatar': '/images/user.png'
    });
  }
});
const app = getApp();

Page({
  data: {
    notifications: [],
    page: 1,
    pageSize: 20,
    hasMore: true,
    loading: false,
    unreadCount: 0,
    filterStatus: 'all'
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }
    this.resetAndLoad();
  },

  resetAndLoad() {
    this.setData({
      notifications: [],
      page: 1,
      hasMore: true
    });
    this.loadNotifications(true);
  },

  async loadNotifications(reset = false) {
    if (this.data.loading) return;

    this.setData({ loading: true });

    try {
      let { page, pageSize, filterStatus } = this.data;
      if (reset) {
        page = 1;
      }

      const params = { page, pageSize };
      if (filterStatus && filterStatus !== 'all') {
        params.status = filterStatus;
      }

      const queryString = Object.keys(params)
        .map(key => `${key}=${encodeURIComponent(params[key])}`)
        .join('&');

      const res = await app.request({
        url: `/notifications?${queryString}`
      });

      const list = (res.data || []).map(item => ({
        ...item,
        createdAtText: app.formatDate(item.createdAt, 'YYYY-MM-DD HH:mm')
      }));

      const newPage = page;
      const notifications = reset
        ? list
        : this.data.notifications.concat(list);

      this.setData({
        notifications,
        page: newPage + (list.length === pageSize ? 1 : 0),
        hasMore: list.length === pageSize,
        unreadCount: res.unreadCount || 0
      });
    } catch (error) {
      console.error('加载通知失败:', error);
      app.showToast('加载通知失败');
    } finally {
      this.setData({ loading: false });
      wx.stopPullDownRefresh();
    }
  },

  loadMore() {
    if (!this.data.hasMore || this.data.loading) return;
    this.loadNotifications(false);
  },

  changeFilterStatus(e) {
    const status = e.currentTarget.dataset.status;
    if (!status || status === this.data.filterStatus) return;
    this.setData({ filterStatus: status }, () => {
      this.resetAndLoad();
    });
  },

  async onNotificationTap(e) {
    const id = e.currentTarget.dataset.id;
    const index = this.data.notifications.findIndex(n => n.id === id);
    if (index === -1) return;

    const notification = this.data.notifications[index];
    if (notification.status === 'read') {
      return;
    }

    try {
      await app.request({
        url: `/notifications/${id}/read`,
        method: 'PUT'
      });

      const key = `notifications[${index}].status`;
      const nextUnread = Math.max(0, this.data.unreadCount - 1);
      this.setData({
        [key]: 'read',
        unreadCount: nextUnread
      });
    } catch (error) {
      console.error('标记已读失败:', error);
      app.showToast('操作失败，请重试');
    }
  },

  async markAllAsRead() {
    if (this.data.unreadCount === 0) return;

    try {
      await app.request({
        url: '/notifications/read-all',
        method: 'PUT'
      });

      const notifications = this.data.notifications.map(item => ({
        ...item,
        status: 'read'
      }));

      this.setData({
        notifications,
        unreadCount: 0
      });

      app.showToast('已全部标记为已读', 'success');
    } catch (error) {
      console.error('全部标记为已读失败:', error);
      app.showToast('操作失败，请重试');
    }
  },

  onPullDownRefresh() {
    this.resetAndLoad();
  }
});

const app = getApp();

Page({
  data: {
    settings: {
      enableBudgetAlert: true,
      enableNotificationBadge: true,
      defaultToCurrentFamily: true
    }
  },

  onLoad() {
    this.loadSettings();
  },

  loadSettings() {
    try {
      const stored = wx.getStorageSync('appSettings');
      if (stored && typeof stored === 'object') {
        this.setData({ settings: { ...this.data.settings, ...stored } });
      }
    } catch (e) {
      console.error('加载应用设置失败:', e);
    }
  },

  saveSettings(partial) {
    const settings = { ...this.data.settings, ...partial };
    this.setData({ settings });
    try {
      wx.setStorageSync('appSettings', settings);
    } catch (e) {
      console.error('保存应用设置失败:', e);
    }
  },

  onToggleBudgetAlert(e) {
    this.saveSettings({ enableBudgetAlert: e.detail.value });
  },

  onToggleNotificationBadge(e) {
    this.saveSettings({ enableNotificationBadge: e.detail.value });
  },

  onToggleDefaultFamily(e) {
    this.saveSettings({ defaultToCurrentFamily: e.detail.value });
  },

  async clearLocalCache() {
    const confirmed = await app.showModal('清理本地缓存', '将清理本地缓存的分类、家庭信息等，不会删除服务器上的数据。');
    if (!confirmed) return;

    try {
      wx.removeStorageSync('categories');
      // 保留 token 和用户信息，这里只清理业务缓存
      wx.removeStorageSync('currentFamily');
      app.showToast('本地缓存已清理', 'success');
    } catch (e) {
      console.error('清理缓存失败:', e);
      app.showToast('清理失败，请稍后重试');
    }
  }
});

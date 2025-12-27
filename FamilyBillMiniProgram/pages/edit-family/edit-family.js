// pages/edit-family/edit-family.js
const app = getApp();

Page({
  data: {
    familyId: null,
    name: '',
    description: '',
    loading: false,
    saving: false
  },

  onLoad(options) {
    const familyIdFromOption = options && options.id ? parseInt(options.id, 10) : null;
    const globalFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
    const familyId = familyIdFromOption || (globalFamily && globalFamily.id) || null;

    if (!familyId) {
      app.showToast('未找到家庭信息');
      setTimeout(() => {
        wx.navigateBack({});
      }, 1500);
      return;
    }

    this.setData({ familyId });
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({ url: '/pages/login/login' });
      return;
    }

    this.loadFamily();
  },

  async loadFamily() {
    if (this.data.loading) return;
    this.setData({ loading: true });

    try {
      const res = await app.request({
        url: '/families'
      });

      const families = res.data || [];
      const family = families.find(f => f.id === this.data.familyId);

      if (!family) {
        app.showToast('家庭不存在或已被删除');
        setTimeout(() => {
          wx.navigateBack({});
        }, 1500);
        return;
      }

      this.setData({
        name: family.name || '',
        description: family.description || ''
      });
    } catch (error) {
      console.error('加载家庭信息失败:', error);
      app.showToast(error.message || '加载家庭信息失败');
    } finally {
      this.setData({ loading: false });
    }
  },

  onNameInput(e) {
    this.setData({ name: e.detail.value });
  },

  onDescriptionInput(e) {
    this.setData({ description: e.detail.value });
  },

  async saveFamily() {
    if (this.data.saving) return;

    const { familyId, name, description } = this.data;
    if (!name || !name.trim()) {
      app.showToast('请输入家庭名称');
      return;
    }

    this.setData({ saving: true });

    try {
      const payload = {
        name: name.trim(),
        description: description != null ? description.trim() : null
      };

      const res = await app.request({
        url: `/families/${familyId}`,
        method: 'PUT',
        data: payload
      });

      const updated = res.data || null;
      if (updated) {
        const currentFamily = {
          id: updated.id,
          name: updated.name,
          description: updated.description,
          inviteCode: updated.inviteCode,
          createdAt: updated.createdAt,
          memberCount: updated.memberCount,
          role: updated.role
        };
        app.globalData.currentFamily = currentFamily;
        wx.setStorageSync('currentFamily', currentFamily);
      }

      app.showToast(res.message || '家庭信息已更新', 'success');

      setTimeout(() => {
        wx.navigateBack({});
      }, 800);
    } catch (error) {
      console.error('更新家庭信息失败:', error);
      app.showToast(error.message || '更新失败，请重试');
    } finally {
      this.setData({ saving: false });
    }
  }
});

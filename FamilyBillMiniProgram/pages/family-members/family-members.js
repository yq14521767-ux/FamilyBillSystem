// pages/family-members/family-members.js
const app = getApp();

Page({
  data: {
    familyId: null,
    members: [],
    currentUserId: null,
    isAdmin: false,
    loading: false,
    showEditNicknameModal: false,
    editingMemberId: null,
    editNicknameValue: ''
  },

  onLoad(options) {
    const familyIdFromOption = options && options.familyId ? parseInt(options.familyId, 10) : null;
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

    this.loadMembers();
  },

  async loadMembers() {
    if (!this.data.familyId) return;
    if (this.data.loading) return;

    this.setData({ loading: true });

    try {
      const res = await app.request({
        url: `/families/${this.data.familyId}/members`
      });

      const rawMembers = res.data || [];
      const members = rawMembers.map(m => ({
        ...m,
        joinDate: app.formatDate(m.joinedAt, 'YYYY-MM-DD'),
        avatarDisplayUrl: app.getAvatarProxyUrl(m.avatar)
      }));

      const { currentUserId, isAdmin } = this.determineCurrentUserState(members);

      this.setData({
        members,
        currentUserId,
        isAdmin
      });
    } catch (error) {
      console.error('加载家庭成员失败:', error);
      app.showToast(error.message || '加载成员失败');
    } finally {
      this.setData({ loading: false });
    }
  },

  determineCurrentUserState(members) {
    const userInfo = app.globalData.userInfo || {};
    let currentUserId = userInfo.id || null;

    if (!currentUserId) {
      const byEmail = userInfo.email && members.find(m => m.user && m.user.email === userInfo.email);
      if (byEmail) {
        currentUserId = byEmail.userId;
      } else {
        const byNickname = userInfo.nickname && members.find(m => m.user && m.user.nickname === userInfo.nickname);
        if (byNickname) {
          currentUserId = byNickname.userId;
        }
      }
    }

    const isAdmin = currentUserId
      ? members.some(m => m.userId === currentUserId && m.role === 1)
      : false;

    return { currentUserId, isAdmin };
  },

  // 切换管理员角色
  async toggleAdmin(e) {
    const memberId = e.currentTarget.dataset.id;
    const isAdminNow = !!e.currentTarget.dataset.isAdmin;

    const target = this.data.members.find(m => m.id === memberId);
    if (!target) return;

    const confirmText = isAdminNow ? '确定要取消该成员的管理员身份吗？' : '确定将该成员设为管理员吗？';
    const confirmed = await app.showModal('确认操作', confirmText);
    if (!confirmed) return;

    try {
      app.showLoading('提交中...');
      await app.request({
        url: `/families/${this.data.familyId}/members/${memberId}/role`,
        method: 'POST',
        data: {
          isAdmin: !isAdminNow
        }
      });

      await this.loadMembers();
      app.showToast('成员角色已更新', 'success');
    } catch (error) {
      console.error('更新成员角色失败:', error);
      app.showToast(error.message || '更新失败');
    } finally {
      app.hideLoading();
    }
  },

  // 移除成员
  async removeMember(e) {
    const memberId = e.currentTarget.dataset.id;
    const target = this.data.members.find(m => m.id === memberId);
    if (!target) return;

    const confirmed = await app.showModal('确认移除', `确定要将 ${target.nickName || target.username} 移出家庭吗？`);
    if (!confirmed) return;

    try {
      app.showLoading('移除中...');
      await app.request({
        url: `/families/${this.data.familyId}/members/${memberId}/remove`,
        method: 'POST'
      });

      await this.loadMembers();
      app.showToast('成员已移除', 'success');
    } catch (error) {
      console.error('移除成员失败:', error);
      app.showToast(error.message || '移除失败');
    } finally {
      app.hideLoading();
    }
  },

  // 打开编辑昵称弹窗
  openEditNickname(e) {
    const memberId = e.currentTarget.dataset.id;
    const target = this.data.members.find(m => m.id === memberId);
    if (!target) return;

    this.setData({
      editingMemberId: memberId,
      editNicknameValue: target.nickname || target.nickName || '',
      showEditNicknameModal: true
    });
  },

  closeEditNicknameModal() {
    this.setData({
      showEditNicknameModal: false,
      editingMemberId: null,
      editNicknameValue: ''
    });
  },

  onEditNicknameInput(e) {
    this.setData({ editNicknameValue: e.detail.value });
  },

  async submitEditNickname() {
    const memberId = this.data.editingMemberId;
    if (!memberId) return;

    try {
      app.showLoading('保存中...');
      await app.request({
        url: `/families/${this.data.familyId}/members/${memberId}/nickname`,
        method: 'PUT',
        data: {
          nickname: this.data.editNicknameValue
        }
      });

      this.setData({ showEditNicknameModal: false, editingMemberId: null, editNicknameValue: '' });
      await this.loadMembers();
      app.showToast('昵称已更新', 'success');
    } catch (error) {
      console.error('更新昵称失败:', error);
      app.showToast(error.message || '更新失败');
    } finally {
      app.hideLoading();
    }
  }
});

// pages/profile/profile.js
const app = getApp();

Page({
  data: {
    userInfo: {},
    joinDate: '',
    userStats: {
    totalBills: 0,
    totalFamilies: 0,
    thisMonthBills: 0,
    thisMonthAmount: '0.00'
    },

    // 弹窗状态
    showEditProfile: false,
    showExportModal: false,

    // 表单数据
    editForm: {
      nickName: '',
      gender: 0,
      phone: ''
    },
    genderOptions: [
      { value: 0, name: '保密' },
      { value: 1, name: '男' },
      { value: 2, name: '女' }
    ],
    genderIndex: 0,
    exportForm: {
      range: 'month',
      format: 'excel'
    },

    // 加载状态
    saveLoading: false,
    exportLoading: false
  },

  onLoad() {
    this.initUserInfo();
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }

    this.initUserInfo();
    this.loadUserStats();
  },

  // 初始化用户信息
  initUserInfo() {
    const userInfo = app.globalData.userInfo || {};
    const joinDate = userInfo.createdAt ? app.formatDate(userInfo.createdAt, 'YYYY-MM-DD') : '';
    const avatarDisplayUrl = app.getAvatarProxyUrl(userInfo.avatarUrl || userInfo.avatar);

    this.setData({
      userInfo: {
        ...userInfo,
        avatarDisplayUrl: avatarDisplayUrl || '/images/user.png'
      },
      joinDate,
      editForm: {
        nickName: userInfo.nickname || '',
        email: userInfo.email || ''
      }
    });
  },

  // 加载用户统计数据
  async loadUserStats() {
    try {
      const res = await app.request({ url: '/users/stats' });
      this.setData({
        userStats: {
          totalBills: res.totalBills || 0,
          totalFamilies: res.totalFamilies || 0,
          thisMonthBills: res.thisMonthBills || 0,
          thisMonthAmount: app.formatAmount(res.thisMonthAmount || 0)
        }
      });
    } catch (error) {
      console.error('加载用户统计失败:', error);
    }
  },

  // 更换头像
  changeAvatar() {
    wx.chooseImage({
      count: 1,
      sizeType: ['compressed'],
      sourceType: ['album', 'camera'],
      success: (res) => {
        const tempFilePath = res.tempFilePaths[0];
        this.uploadAvatar(tempFilePath);
      }
    });
  },

  // 头像加载失败处理
  onAvatarLoadError(e) {
    console.error('头像加载失败:', e.detail);
    console.error('失败的URL:', this.data.userInfo.avatarDisplayUrl || this.data.userInfo.avatarUrl);
    this.setData({ 'userInfo.avatarDisplayUrl': '/images/user.png' });
  },

  // 上传头像
  async uploadAvatar(filePath) {
    if (!app.globalData.token) {
      wx.showToast({ title: '用户未登录，请先登录', icon: 'none' });
      return;
    }

    const oldAvatarDisplay = this.data.userInfo.avatarDisplayUrl || '/images/user.png';
    this.setData({ 'userInfo.avatarDisplayUrl': filePath }); // 临时显示本地图片
    app.showLoading('上传中...');

    try {
      // 获取文件信息
      const fileInfo = await new Promise((resolve, reject) => {
        wx.getFileInfo({
          filePath: filePath,
          success: resolve,
          fail: reject
        });
      });

      // 检查文件大小（限制为2MB）
      if (fileInfo.size > 2 * 1024 * 1024) {
        throw new Error('图片大小不能超过2MB');
      }

      const res = await new Promise((resolve, reject) => {
        wx.uploadFile({
          url: `${app.globalData.baseUrl}/auth/upload-avatar`,
          filePath: filePath,
          name: 'file', // 必须与后端 IFormFile file 参数名一致
          header: {
            'Authorization': `Bearer ${app.globalData.token}`,
            'Accept': 'application/json'
          },
          success: (uploadRes) => {
            let data;
            try {
              data = JSON.parse(uploadRes.data);
              if (!data) throw new Error('服务器返回空响应');
              resolve({ statusCode: uploadRes.statusCode, data });
            } catch (e) {
              reject(new Error('解析服务器响应失败'));
            }
          },
          fail: (err) => {
            console.error('上传请求失败:', err);
            reject(new Error(err.errMsg || '上传请求失败'));
          }
        });
      });

      if (res.statusCode === 200 && res.data && res.data.success) {
        // 更新用户信息中的头像URL
        let avatarUrl = res.data.data || '';
        // 添加时间戳避免缓存问题
        if (avatarUrl && avatarUrl !== '/images/user.png') {
          avatarUrl = `${avatarUrl}?t=${Date.now()}`;
        }

        const avatarDisplayUrl = app.getAvatarProxyUrl(avatarUrl);
        
        const userInfo = { 
          ...this.data.userInfo, 
          avatarUrl,
          avatarDisplayUrl: avatarDisplayUrl || '/images/user.png'
        };
        
        // 更新全局用户信息和本地存储
        app.globalData.userInfo = userInfo;
        wx.setStorageSync('userInfo', userInfo);
        
        // 更新页面数据
        this.setData({ 
          userInfo,
          'userInfo.avatarDisplayUrl': userInfo.avatarDisplayUrl
        });
        

        
        wx.showToast({ 
          title: res.data.message || '头像上传成功', 
          icon: 'success' 
        });
        
        return avatarUrl;
      } else {
        throw new Error(res.data?.message || `上传失败，状态码: ${res.statusCode}`);
      }
    } catch (error) {
      console.error('上传头像失败:', error);
      this.setData({ 
        'userInfo.avatarDisplayUrl': oldAvatarDisplay
      });
      wx.showToast({ 
        title: error.message || '上传失败，请重试', 
        icon: 'none' 
      });
      throw error;
    } finally {
      app.hideLoading();
    }
  },

  // 显示编辑资料弹窗
  editProfile() {
    const user = this.data.userInfo;
    const genderIndex = this.data.genderOptions.findIndex(item => item.value === (user.gender ?? 0));
    this.setData({
      showEditProfile: true,
      'editForm.nickName': user.nickname || '',
      'editForm.gender': user.gender ?? 0,
      'editForm.phone': user.phone || '',
      genderIndex: genderIndex >= 0 ? genderIndex : 0
    });
  },

  hideEditProfile() { this.setData({ showEditProfile: false }); },

  onNickNameInput(e) { this.setData({ 'editForm.nickName': e.detail.value }); },
  onGenderChange(e) {
    const index = e.detail.value;
    const gender = this.data.genderOptions[index].value;
    this.setData({ 'editForm.gender': gender, genderIndex: index });
  },
  onPhoneInput(e) { this.setData({ 'editForm.phone': e.detail.value.replace(/[^\d]/g, '') }); },

  async saveProfile() {
    if (this.data.saveLoading) return;
    const { nickName, gender, phone } = this.data.editForm;

    if (!nickName || !nickName.trim()) { wx.showToast({ title: '请输入昵称', icon: 'none' }); return; }
    if (nickName.length > 50) { wx.showToast({ title: '昵称不能超过50个字符', icon: 'none' }); return; }
    if (phone && !/^1[3-9]\d{9}$/.test(phone)) { wx.showToast({ title: '手机号格式不正确', icon: 'none' }); return; }

    this.setData({ saveLoading: true });
    try {
      const res = await app.request({ url: '/auth/profile', method: 'PUT', data: { nickname: nickName.trim(), gender, phone: phone || '' } });
      if (res && res.success && res.data) {
        const updatedUser = res.data;
        // 更新用户信息，确保使用后端返回的字段
        const userInfo = { 
          ...this.data.userInfo, 
          nickname: updatedUser.nickname,
          gender: updatedUser.gender,
          phone: updatedUser.phone
        };
        app.globalData.userInfo = userInfo;
        wx.setStorageSync('userInfo', userInfo);
        this.setData({ userInfo, showEditProfile: false, saveLoading: false });
        wx.showToast({ title: '资料更新成功', icon: 'success' });
        return true;
      } else throw new Error(res.message || '更新失败');
    } catch (error) {
      console.error('更新资料失败:', error);
      wx.showToast({ title: error.message || '更新失败，请重试', icon: 'none' });
      this.setData({ saveLoading: false });
      throw error;
    }
  },

  // 跳转到个人资料详情页
  goToProfileDetail() {
    wx.navigateTo({ url: '/pages/profile-detail/profile-detail' });
  },

  goToCategories() { wx.navigateTo({ url: '/pages/categories/categories' }); },
  goToBudget() { wx.navigateTo({ url: '/pages/budget/budget' }); },
  exportData() { this.setData({ showExportModal: true, exportForm: { range: 'month', format: 'excel' } }); },
  hideExportModal() { this.setData({ showExportModal: false }); },
  selectExportRange(e) { this.setData({ 'exportForm.range': e.currentTarget.dataset.range }); },
  selectExportFormat(e) { this.setData({ 'exportForm.format': e.currentTarget.dataset.format }); },

  async confirmExport() {
    const { exportForm, exportLoading } = this.data;
    if (exportLoading) return;

    const queryString = Object.keys(exportForm)
      .map(key => `${key}=${encodeURIComponent(exportForm[key])}`)
      .join('&');

    const url = `${app.globalData.baseUrl}/bills/export?${queryString}`;
    const token = app.globalData.token;

    this.setData({ exportLoading: true });

    wx.downloadFile({
      url,
      header: token ? { 'Authorization': `Bearer ${token}` } : {},
      success: (downloadRes) => {
        if (downloadRes.statusCode === 200) {
          const tempFilePath = downloadRes.tempFilePath;
          const fs = wx.getFileSystemManager();
          fs.saveFile({
            tempFilePath,
            success: (saveRes) => {
              const savedFilePath = saveRes.savedFilePath || tempFilePath;
              wx.openDocument({
                filePath: savedFilePath,
                showMenu: true,
                success: () => {
                  app.showToast('导出成功，可在预览界面保存或分享', 'success');
                },
                fail: () => {
                  app.showToast('导出文件已保存，但打开失败');
                },
                complete: () => {
                  this.setData({ exportLoading: false, showExportModal: false });
                }
              });
            },
            fail: () => {
              // 即使保存失败，也尝试直接打开临时文件
              wx.openDocument({
                filePath: tempFilePath,
                showMenu: true,
                success: () => {
                  app.showToast('导出成功，可在预览界面保存或分享', 'success');
                },
                fail: () => {
                  app.showToast('保存失败');
                },
                complete: () => {
                  this.setData({ exportLoading: false, showExportModal: false });
                }
              });
            }
          });
        } else {
          console.error('导出接口返回错误状态码:', downloadRes.statusCode);
          app.showToast('导出失败，请重试');
          this.setData({ exportLoading: false, showExportModal: false });
        }
      },
      fail: (err) => {
        console.error('下载导出文件失败:', err);
        app.showToast('下载失败，请检查网络');
        this.setData({ exportLoading: false, showExportModal: false });
      }
    });
  },

  goToNotifications() { wx.navigateTo({ url: '/pages/notifications/notifications' }); },
  goToSettings() { wx.navigateTo({ url: '/pages/settings/settings' }); },
  goToHelp() { wx.navigateTo({ url: '/pages/help/help' }); },
  goToAbout() { wx.navigateTo({ url: '/pages/about/about' }); },

  async logout() {
    const confirmed = await app.showModal('确认退出', '确定要退出登录吗？');
    if (!confirmed) return;
    try {
      const res = await app.request({ url: '/auth/logout', method: 'POST' });
      if (res && res.success) {
        app.showToast(res.message || '退出成功', 'success');
      } else {
        app.showToast((res && res.message) || '退出失败', 'none');
      }
    } catch (error) {
      console.error('退出登录失败:', error);
      app.showToast(error.message || '退出失败，请重试', 'none');
    } finally {
      app.logout();
    }
  },

  onPullDownRefresh() { this.loadUserStats().finally(() => wx.stopPullDownRefresh()); }
});

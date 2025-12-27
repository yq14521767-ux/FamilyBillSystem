// pages/profile-detail/profile-detail.js
const app = getApp();

Page({
  data: {
    user: null,
    genderText: '',
    statusText: '',
    createdAtText: '',
    updatedAtText: '',
    lastLoginAtText: ''
  },

  onLoad() {
    const user = app.globalData.userInfo || wx.getStorageSync('userInfo') || {};
    const rawAvatar = user.avatar || user.avatarUrl;
    const avatarDisplayUrl = app.getAvatarProxyUrl(rawAvatar);

    const genderMap = {
      0: '保密',
      1: '男',
      2: '女'
    };

    const genderText = (user.gender === null || user.gender === undefined)
      ? '未设置'
      : (genderMap[user.gender] || '未设置');

    const statusText = user.status === 'frozen' ? '已冻结' : '正常';

    const createdAtText = user.createdAt ? app.formatDate(user.createdAt, 'YYYY-MM-DD HH:mm:ss') : '—';
    const updatedAtText = user.updatedAt ? app.formatDate(user.updatedAt, 'YYYY-MM-DD HH:mm:ss') : '—';
    const lastLoginAtText = user.lastLoginAt ? app.formatDate(user.lastLoginAt, 'YYYY-MM-DD HH:mm:ss') : '—';

    this.setData({
      user: {
        ...user,
        avatarDisplayUrl: avatarDisplayUrl || '/images/user.png'
      },
      genderText,
      statusText,
      createdAtText,
      updatedAtText,
      lastLoginAtText
    });
  }
});

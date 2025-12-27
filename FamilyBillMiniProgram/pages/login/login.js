// pages/login/login.js
const app = getApp();

Page({
  data: {
    email: '',
    password: '',
    loading: false,
    showRegisterModal: false,
    registerLoading: false,
    codeSending: false,
    countdown: 0,
    registerForm: {
      email: '',
      verificationCode: '',
      password: '',
      nickName: ''
    }
  },

  onLoad() {
    // 如果已经登录，直接跳转到首页
    if (app.globalData.token) {
      wx.switchTab({
        url: '/pages/index/index'
      });
    }
  },

  // 邮箱输入
  onEmailInput(e) {
    this.setData({
      email: e.detail.value
    });
  },

  // 密码输入
  onPasswordInput(e) {
    this.setData({
      password: e.detail.value
    });
  },

  // 普通登录
  onLogin() {
    const { email, password } = this.data;

    if (!email.trim()) {
      app.showToast('请输入邮箱');
      return;
    }
    
    // 简单的邮箱格式验证
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      app.showToast('邮箱格式不正确');
      return;
    }

    if (!password.trim()) {
      app.showToast('请输入密码');
      return;
    }

    this.setData({ loading: true });

    wx.request({
      url: `${app.globalData.baseUrl}/auth/login`,
      method: 'POST',
      data: {
        email: email.trim(),
        password: password
      },
      header: {
        'Content-Type': 'application/json'
      },
      success: (res) => {
        if (res.statusCode === 200 && res.data.token && res.data.refreshToken) {
          const { token, refreshToken, user } = res.data;
          
          // 保存登录信息
          app.globalData.token = token;
          app.globalData.refreshToken = refreshToken;
          app.globalData.userInfo = user;
          wx.setStorageSync('token', token);
          wx.setStorageSync('refreshToken', refreshToken);
          wx.setStorageSync('userInfo', user);

          app.showToast('登录成功', 'success');
          
          // 跳转到首页
          setTimeout(() => {
            wx.switchTab({
              url: '/pages/index/index'
            });
          }, 1000);
        } else {
          app.showToast(res.data?.message || '登录失败');
        }
      },
      fail: (error) => {
        console.error('登录失败:', error);
        app.showToast('网络错误，请重试');
      },
      complete: () => {
        this.setData({ loading: false });
      }
    });
  },

  // 微信登录
  onWeChatLogin(e) {
    if (!e.detail.userInfo) {
      app.showToast('需要授权才能使用微信登录');
      return;
    }

    this.setData({ loading: true });

    // 获取登录凭证
    wx.login({
      success: (loginRes) => {
        if (!loginRes.code) {
          app.showToast('获取登录凭证失败');
          this.setData({ loading: false });
          return;
        }

        // 调用后端微信登录接口
        wx.request({
          url: `${app.globalData.baseUrl}/auth/wechat-login`,
          method: 'POST',
          data: {
            code: loginRes.code,
            nickName: e.detail.userInfo.nickName,
            avatar: e.detail.userInfo.avatarUrl
          },
          header: {
            'Content-Type': 'application/json'
          },
          success: (res) => {
            if (res.statusCode === 200 && res.data.token && res.data.refreshToken) {
              const { token, refreshToken, user } = res.data;
              
              // 保存登录信息
              app.globalData.token = token;
              app.globalData.refreshToken = refreshToken;
              app.globalData.userInfo = user;
              wx.setStorageSync('token', token);
              wx.setStorageSync('refreshToken', refreshToken);
              wx.setStorageSync('userInfo', user);

              app.showToast('登录成功', 'success');
              
              // 跳转到首页
              setTimeout(() => {
                wx.switchTab({
                  url: '/pages/index/index'
                });
              }, 1000);
            } else {
              app.showToast(res.data.message || '微信登录失败');
            }
          },
          fail: (error) => {
            console.error('微信登录失败:', error);
            app.showToast('微信登录失败，请重试');
          },
          complete: () => {
            this.setData({ loading: false });
          }
        });
      },
      fail: (error) => {
        console.error('获取登录凭证失败:', error);
        app.showToast('获取登录凭证失败');
        this.setData({ loading: false });
      }
    });
  },

  // 显示注册弹窗
  onRegister() {
    this.setData({
      showRegisterModal: true,
      countdown: 0,
      registerForm: {
        email: '',
        verificationCode: '',
        password: '',
        confirmPassword: '',
        nickName: ''
      }
    });
  },

  // 隐藏注册弹窗
  hideRegisterModal() {
    this.setData({
      showRegisterModal: false
    });
  },

  // 注册表单输入

  onRegisterEmailInput(e) {
    this.setData({
      'registerForm.email': e.detail.value
    });
  },

  onRegisterPasswordInput(e) {
    this.setData({
      'registerForm.password': e.detail.value
    });
  },

  onRegisterNickNameInput(e) {
    this.setData({
      'registerForm.nickName': e.detail.value
    });
  },

  // 确认密码输入
  onRegisterConfirmPasswordInput(e) {
    this.setData({
      'registerForm.confirmPassword': e.detail.value
    });
  },

  onRegisterCodeInput(e) {
    this.setData({
      'registerForm.verificationCode': e.detail.value
    });
  },

  // 发送验证码
  onSendCode() {
    const { registerForm } = this.data;

    if (!registerForm.email.trim()) {
      app.showToast('请先输入邮箱');
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(registerForm.email)) {
      app.showToast('请输入有效的邮箱地址');
      return;
    }

    this.setData({ codeSending: true });

    wx.request({
      url: `${app.globalData.baseUrl}/auth/send-code`,
      method: 'POST',
      data: {
        email: registerForm.email.trim()
      },
      header: {
        'Content-Type': 'application/json'
      },
      success: (res) => {
        if (res.statusCode === 200) {
          app.showToast('验证码已发送', 'success');
          
          // 开始倒计时
          this.setData({ countdown: 60 });
          this.startCountdown();
        } else {
          app.showToast(res.data.message || '发送失败');
        }
      },
      fail: (error) => {
        console.error('发送验证码失败:', error);
        app.showToast('网络错误，请重试');
      },
      complete: () => {
        this.setData({ codeSending: false });
      }
    });
  },

  // 倒计时
  startCountdown() {
    const timer = setInterval(() => {
      const countdown = this.data.countdown - 1;
      if (countdown <= 0) {
        clearInterval(timer);
        this.setData({ countdown: 0 });
      } else {
        this.setData({ countdown });
      }
    }, 1000);
  },

  // 提交注册
  onRegisterSubmit() {
    const { registerForm } = this.data;

    // 表单验证
    if (!registerForm.email.trim()) {
      app.showToast('请输入邮箱');
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(registerForm.email)) {
      app.showToast('请输入有效的邮箱地址');
      return;
    }

    if (!registerForm.verificationCode.trim()) {
      app.showToast('请输入验证码');
      return;
    }

    if (registerForm.verificationCode.length !== 6) {
      app.showToast('验证码必须为6位数字');
      return;
    }

    if (!registerForm.password.trim()) {
      app.showToast('请输入密码');
      return;
    }

    if (registerForm.password.length < 6) {
      app.showToast('密码至少需要6位');
      return;
    }

    // 验证两次密码是否一致
    if (registerForm.password !== registerForm.confirmPassword) {
      app.showToast('两次输入的密码不一致');
      return;
    }

    this.setData({ registerLoading: true });

    // 使用邮箱前缀作为默认昵称
    const defaultNickname = registerForm.email.split('@')[0];

    wx.request({
      url: `${app.globalData.baseUrl}/auth/register`,
      method: 'POST',
      data: {
        email: registerForm.email.trim(),
        verificationCode: registerForm.verificationCode.trim(),
        password: registerForm.password.trim(),
        nickName: registerForm.nickName.trim() || defaultNickname
      },
      header: {
        'Content-Type': 'application/json'
      },
      success: (res) => {
        if (res.statusCode === 200 && res.data.token && res.data.refreshToken) {
          const { token, refreshToken, user } = res.data;
          
          // 保存登录信息
          app.globalData.token = token;
          app.globalData.refreshToken = refreshToken;
          app.globalData.userInfo = user;
          wx.setStorageSync('token', token);
          wx.setStorageSync('refreshToken', refreshToken);
          wx.setStorageSync('userInfo', user);

          app.showToast('注册成功', 'success');
          
          // 隐藏弹窗并跳转到首页
          this.hideRegisterModal();
          setTimeout(() => {
            wx.switchTab({
              url: '/pages/index/index'
            });
          }, 1000);
        } else {
          app.showToast(res.data?.message || '注册失败');
        }
      },
      fail: (error) => {
        console.error('注册失败:', error);
        app.showToast('网络错误，请重试');
      },
      complete: () => {
        this.setData({ registerLoading: false });
      }
    });
  }
});
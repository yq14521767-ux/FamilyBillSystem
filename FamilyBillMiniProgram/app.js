// app.js
App({
  globalData: {
    userInfo: null,
    token: null,
    refreshToken: null,
    baseUrl: 'https://familysys.hejiancheng.xyz/api',
    currentFamily: null,
    categories: [],
    systemInfo: null,
    isRefreshingToken: false,
    requestQueue: []
  },

  onLaunch() {
    wx.getSystemInfo({
      success: (res) => {
        this.globalData.systemInfo = res;
      }
    });
    this.checkLoginStatus();
  },

  checkLoginStatus() {
    const token = wx.getStorageSync('token');
    const refreshToken = wx.getStorageSync('refreshToken');
    const userInfo = wx.getStorageSync('userInfo');
    

    if (token && refreshToken && userInfo) {
      this.globalData.token = token;
      this.globalData.refreshToken = refreshToken;
      this.globalData.userInfo = userInfo;

       // 同步当前家庭信息，确保全局 currentFamily 与本地缓存一致
      const currentFamily = wx.getStorageSync('currentFamily');
      if (currentFamily && currentFamily.id) {
        this.globalData.currentFamily = currentFamily;
      } else {
        this.globalData.currentFamily = null;
      }
      
      // 验证Token有效性
      this.validateToken();
    } else {
      this.clearAuthData();
    }
  },

  // 验证Token有效性
  async validateToken() {
    try {
      await this.request({
        url: '/auth/validate-token',
        method: 'POST'
      });
    } catch (err) {
      // Token无效，清空数据
      this.clearAuthData();
    }
  },

  clearAuthData() {
    this.globalData.token = null;
    this.globalData.refreshToken = null;
    this.globalData.userInfo = null;
    this.globalData.currentFamily = null;
    wx.removeStorageSync('token');
    wx.removeStorageSync('refreshToken');
    wx.removeStorageSync('userInfo');
    wx.removeStorageSync('currentFamily');
  },


  async login(loginData) {
    return new Promise((resolve, reject) => {
      wx.request({
        url: `${this.globalData.baseUrl}/auth/wechat-login`,
        method: 'POST',
        data: loginData,
        header: { 'Content-Type': 'application/json' },
        success: (res) => {
          if (res.statusCode === 200 && res.data.token && res.data.refreshToken && res.data.user) {
            const { token, refreshToken, user } = res.data;
            this.globalData.token = token;
            this.globalData.refreshToken = refreshToken;
            this.globalData.userInfo = user;
            wx.setStorageSync('token', token);
            wx.setStorageSync('refreshToken', refreshToken);
            wx.setStorageSync('userInfo', user);
            resolve(res.data);
          } else {
            reject(res.data || { message: '登录失败' });
          }
        },
        fail: (err) => reject(err)
      });
    });
  },

  logout() {
    this.clearAuthData();
    wx.reLaunch({ url: '/pages/login/login' });
  },

  async request(options) {
    return new Promise((resolve, reject) => {
      const url = `${this.globalData.baseUrl}${options.url}`;
      
      const headers = {
        'Content-Type': 'application/json',
        ...options.header
      };
      
      // 只在有token时添加Authorization头
      if (this.globalData.token) {
        const token = this.globalData.token;
        const parts = token.split('.');
        
        // 验证Token格式
        if (parts.length !== 3) {
          this.log.error('Token格式错误，将清除认证信息');
          this.clearAuthData();
          return Promise.reject({ message: '认证信息无效，请重新登录' });
        }
        
        headers['Authorization'] = `Bearer ${token}`;
      }

      wx.request({
        url,
        method: options.method || 'GET',
        data: options.data || {},
        header: headers,
        success: async (res) => {
          if (res.statusCode === 200) {
            return resolve(res.data);
          }

          // Token过期，尝试刷新
          if (res.statusCode === 401 && !options._isRetry) {
            try {
              await this.handleTokenRefresh();
              // 刷新成功，重试请求（标记为重试避免无限循环）
              options._isRetry = true;
              const retryRes = await this.request(options);
              resolve(retryRes);
            } catch (err) {
              this.log.error('Token刷新失败', err);
              this.logout();
              reject({ message: '登录已过期，请重新登录' });
            }
            return;
          }

          this.log.error(`请求失败 ${res.statusCode}`, res.data);
          reject(res.data || { message: '请求失败' });
        },
        fail: (err) => {
          this.log.error('网络请求失败', err);
          reject({ message: err.errMsg || '网络请求失败' });
        }
      });
    });
  },

  async handleTokenRefresh() {
    // 防止并发刷新
    if (this.globalData.isRefreshingToken) {
      return new Promise((resolve, reject) => {
        const checkInterval = setInterval(() => {
          if (!this.globalData.isRefreshingToken) {
            clearInterval(checkInterval);
            if (this.globalData.token) {
              resolve();
            } else {
              reject(new Error('Token刷新失败'));
            }
          }
        }, 100);
        
        // 最多等待10秒
        setTimeout(() => {
          clearInterval(checkInterval);
          reject(new Error('Token刷新超时'));
        }, 10000);
      });
    }

    this.globalData.isRefreshingToken = true;
    
    try {
      const refreshToken = this.globalData.refreshToken;
      
      if (!refreshToken) {
        throw new Error('RefreshToken不存在，请重新登录');
      }

      // 检查RefreshToken格式
      const parts = refreshToken.split('.');
      if (parts.length !== 3) {
        throw new Error('RefreshToken格式错误');
      }

      const refreshRes = await new Promise((resolve, reject) => {
        wx.request({
          url: `${this.globalData.baseUrl}/auth/refresh-token`,
          method: 'POST',
          data: { refreshToken },
          header: { 'Content-Type': 'application/json' },
          success: (res) => {
            if (res.statusCode === 200 && res.data.token && res.data.refreshToken) {
              resolve(res.data);
            } else {
              reject(new Error(res.data?.message || '刷新Token失败'));
            }
          },
          fail: (err) => {
            reject(new Error(err.errMsg || '网络请求失败'));
          }
        });
      });

      // 更新Token
      this.globalData.token = refreshRes.token;
      this.globalData.refreshToken = refreshRes.refreshToken;
      this.globalData.userInfo = refreshRes.user;
      
      wx.setStorageSync('token', refreshRes.token);
      wx.setStorageSync('refreshToken', refreshRes.refreshToken);
      wx.setStorageSync('userInfo', refreshRes.user);
      
    } catch (err) {
      this.log.error('Token刷新失败', err);
      this.clearAuthData();
      throw err;
    } finally {
      this.globalData.isRefreshingToken = false;
    }
  },

  // UI 辅助函数
  showLoading(title = '加载中...') { wx.showLoading({ title, mask: true }); },
  hideLoading() { wx.hideLoading(); },
  showToast(title, icon = 'none', duration = 2000) { wx.showToast({ title, icon, duration }); },
  showModal(title, content) {
    return new Promise((resolve) => wx.showModal({ title, content, success: res => resolve(res.confirm) }));
  },

  // 工具函数
  formatDate(date, format = 'YYYY-MM-DD HH:mm') {
    const d = new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const hour = String(d.getHours()).padStart(2, '0');
    const minute = String(d.getMinutes()).padStart(2, '0');
    const second = String(d.getSeconds()).padStart(2, '0');
    return format.replace('YYYY', year).replace('MM', month).replace('DD', day)
                 .replace('HH', hour).replace('mm', minute).replace('ss', second);
  },

  formatAmount(amount) { return parseFloat(amount).toFixed(2); },

  // 将头像原始URL转换为可在小程序中使用的代理HTTPS地址
  getAvatarProxyUrl(rawUrl) {
    if (!rawUrl) {
      return '/images/user.png';
    }

    // 保留小程序本地或相对路径（如 /images/user.png）
    if (rawUrl.indexOf('http://') !== 0 && rawUrl.indexOf('https://') !== 0) {
      return rawUrl;
    }

    // 已经是 HTTPS 的链接，直接使用
    if (rawUrl.indexOf('https://') === 0) {
      return rawUrl;
    }

    // 对于 HTTP 远程头像，统一走后端代理
    const encoded = encodeURIComponent(rawUrl);
    return `${this.globalData.baseUrl}/auth/avatar-proxy?url=${encoded}`;
  },
  
  // 日志工具
  log: {
    debug: function(...args) {
      if (__wxConfig && __wxConfig.debug) {
        console.debug('[DEBUG]', ...args);
      }
    },
    info: function(...args) {
      console.log('[INFO]', ...args);
    },
    error: function(...args) {
      console.error('[ERROR]', ...args);
      // 可以在这里添加错误上报逻辑
    }
  }
});

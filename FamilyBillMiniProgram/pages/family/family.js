// pages/family/family.js
const app = getApp();

Page({
  data: {
    currentFamily: null,
    familyMembers: [],
    myFamilies: [],
    budgetSummary: null,
    budgetDetails: [],
    showBudgetDetail: false,
    inviteCode: '',
    
    // 弹窗状态
    showCreateFamilyModal: false,
    showJoinFamilyModal: false,
    showInviteModal: false,
    showFamilyActions: false,
    
    // 表单数据
    createForm: {
      name: '',
      familyname:'',
      description: ''
    },
    joinForm: {
      inviteCode: '',
      nickname: ''
    },
    
    // 加载状态
    createLoading: false,
    joinLoading: false,
    canManageCurrentFamily: false
  },

  onLoad() {
    // 从缓存获取当前家庭
    const currentFamily = wx.getStorageSync('currentFamily');
    if (currentFamily) {
      this.setData({ currentFamily });
    }
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }
    
    this.loadData();
  },

  // 加载数据
  async loadData() {
    try {
      await Promise.all([
        this.loadMyFamilies(),
        this.loadCurrentFamilyData()
      ]);
    } catch (error) {
      console.error('加载家庭数据失败:', error);
    }
  },

  // 加载我的家庭列表
  async loadMyFamilies() {
    try {
      const res = await app.request({
        url: '/families'
      });

      const families = res.data || [];
      let { currentFamily } = this.data;

      if (families.length === 0) {
        currentFamily = null;
        app.globalData.currentFamily = null;
        wx.removeStorageSync('currentFamily');
      } else {
        if (currentFamily) {
          const matched = families.find(f => f.id === currentFamily.id);
          if (matched) {
            currentFamily = matched;
          } else {
            currentFamily = families[0];
          }
        } else {
          currentFamily = families[0];
        }
        app.globalData.currentFamily = currentFamily;
        wx.setStorageSync('currentFamily', currentFamily);
      }

      const canManageCurrentFamily =
        !!currentFamily && (currentFamily.role === 'admin' || currentFamily.role === 1);

      this.setData({
        myFamilies: families,
        currentFamily,
        canManageCurrentFamily
      });
    } catch (error) {
      console.error('加载家庭列表失败:', error);
    }
  },

  // 加载当前家庭数据
  async loadCurrentFamilyData() {
    const { currentFamily } = this.data;
    if (!currentFamily) return;
    
    try {
      await Promise.all([
        this.loadFamilyMembers(),
        this.loadBudgetSummary()
      ]);
    } catch (error) {
      console.error('加载当前家庭数据失败:', error);
    }
  },

  // 加载家庭成员
  async loadFamilyMembers() {
    try {
      const res = await app.request({
        url: `/families/${this.data.currentFamily.id}/members`
      });
      
      const members = (res.data || []).map(member => ({
        ...member,
        joinDate: app.formatDate(member.joinedAt, 'YYYY-MM-DD'),
        avatarDisplayUrl: app.getAvatarProxyUrl(member.avatar)
      }));
      
      this.setData({
        familyMembers: members
      });
    } catch (error) {
      console.error('加载家庭成员失败:', error);
    }
  },

  // 加载预算概览
  async loadBudgetSummary() {
    try {
      const now = new Date();
      const year = now.getFullYear();
      const month = now.getMonth() + 1;
      
      const res = await app.request({
        url: `/budgets/summary?year=${year}&month=${month}&familyId=${this.data.currentFamily.id}`
      });
      
      if (res && res.budgets && res.budgets.length > 0) {
        const totalBudget = res.budgets.reduce((sum, item) => sum + item.amount, 0);
        const totalUsed = res.budgets.reduce((sum, item) => sum + item.usedAmount, 0);
        const remaining = totalBudget - totalUsed;
        const usagePercentage = totalBudget > 0 ? Math.round((totalUsed / totalBudget) * 100) : 0;
        
        const details = (res.categoryBudgets || []).map(item => ({
          categoryId: item.categoryId,
          categoryName: item.categoryName || '未分类',
          categoryIcon: item.categoryIcon,
          categoryColor: item.categoryColor || '#666666',
          budget: app.formatAmount(item.budget),
          spent: app.formatAmount(item.spent),
          remaining: app.formatAmount(item.remaining),
          utilizationRate: item.utilizationRate,
          isOverBudget: item.isOverBudget
        }));

        this.setData({
          budgetSummary: {
            totalBudget: app.formatAmount(totalBudget),
            totalUsed: app.formatAmount(totalUsed),
            remaining: app.formatAmount(remaining),
            usagePercentage
          },
          budgetDetails: details
        });
      } else {
        // 没有预算数据时清空预算汇总
        this.setData({
          budgetSummary: null,
          budgetDetails: [],
          showBudgetDetail: false
        });
      }
    } catch (error) {
      console.error('加载预算概览失败:', error);
    }
  },

  // 切换预算明细展开/收起
  toggleBudgetDetail() {
    if (!this.data.budgetSummary) return;
    this.setData({
      showBudgetDetail: !this.data.showBudgetDetail
    });
  },

  // 切换家庭
  async switchFamily(e) {
    const family = e.currentTarget.dataset.family;
    
    if (family.id === this.data.currentFamily?.id) return;
    
    try {
      app.showLoading('切换中...');
      
      // 保存到全局数据和本地存储
      app.globalData.currentFamily = family;
      wx.setStorageSync('currentFamily', family);
      
      this.setData({
        currentFamily: family,
        canManageCurrentFamily: family.role === 'admin' || family.role === 1
      });
      
      // 重新加载当前家庭数据
      await this.loadCurrentFamilyData();
      
      app.showToast('切换成功', 'success');
    } catch (error) {
      console.error('切换家庭失败:', error);
      app.showToast('切换失败，请重试');
    } finally {
      app.hideLoading();
    }
  },

  // 显示创建家庭弹窗
  showCreateFamilyModal() {
    const userInfo = app.globalData.userInfo || {};
    const defaultNickname = userInfo.nickname || userInfo.nickName || '';
    this.setData({
      showCreateFamilyModal: true,
      createForm: {
        name: '',
        familyname: defaultNickname,
        description: ''
      }
    });
  },

  // 隐藏创建家庭弹窗
  hideCreateFamilyModal() {
    this.setData({
      showCreateFamilyModal: false
    });
  },

  // 创建家庭表单输入
  onCreateNameInput(e) {
    this.setData({
      'createForm.name': e.detail.value
    });
  },

  onCreateFamilyNameInput(e){
    this.setData({
      'createForm.familyname':e.detail.value
    });
  },

  onCreateDescriptionInput(e) {
    this.setData({
      'createForm.description': e.detail.value
    });
  },

  // 创建家庭
  async createFamily() {
    const { createForm } = this.data;
    
    if (!createForm.name.trim()) {
      app.showToast('请输入家庭名称');
      return;
    }
    
    this.setData({ createLoading: true });
    
    try {
      const familyData = {
        name: createForm.name.trim(),
        description: createForm.description.trim() || null,
        creatorNickname: createForm.familyname ? createForm.familyname.trim() : null
      };
      
      const res = await app.request({
        url: '/families',
        method: 'POST',
        data: familyData
      });
      
      // 设置为当前家庭
      app.globalData.currentFamily = res.data;
      wx.setStorageSync('currentFamily', res.data);
      
      this.setData({
        currentFamily: res.data,
        showCreateFamilyModal: false,
        canManageCurrentFamily: true
      });
      
      // 重新加载数据
      await this.loadData();
      
      app.showToast('家庭创建成功', 'success');
    } catch (error) {
      console.error('创建家庭失败:', error);
      app.showToast(error.message || '创建失败，请重试');
    } finally {
      this.setData({ createLoading: false });
    }
  },

  // 显示加入家庭弹窗
  showJoinFamilyModal() {
    const userInfo = app.globalData.userInfo || {};
    const defaultNickname = userInfo.nickname || userInfo.nickName || '';
    this.setData({
      showJoinFamilyModal: true,
      joinForm: {
        inviteCode: '',
        nickname: defaultNickname
      }
    });
  },

  // 隐藏加入家庭弹窗
  hideJoinFamilyModal() {
    this.setData({
      showJoinFamilyModal: false
    });
  },

  // 加入家庭表单输入
  onJoinCodeInput(e) {
    this.setData({
      'joinForm.inviteCode': e.detail.value.toUpperCase()
    });
  },

  onJoinNicknameInput(e) {
    this.setData({
      'joinForm.nickname': e.detail.value
    });
  },

  // 加入家庭
  async joinFamily() {
    const { joinForm } = this.data;
    
    if (!joinForm.inviteCode.trim()) {
      app.showToast('请输入邀请码');
      return;
    }
    
    if (joinForm.inviteCode.length !== 6) {
      app.showToast('邀请码应为6位');
      return;
    }
    
    this.setData({ joinLoading: true });
    
    try {
      const res = await app.request({
        url: '/families/join',
        method: 'POST',
        data: {
          inviteCode: joinForm.inviteCode.trim(),
          nickname: joinForm.nickname ? joinForm.nickname.trim() : null
        }
      });
      
      // 设置为当前家庭
      app.globalData.currentFamily = res.data;
      wx.setStorageSync('currentFamily', res.data);
      
      this.setData({
        currentFamily: res.data,
        showJoinFamilyModal: false,
        canManageCurrentFamily: false
      });
      
      // 重新加载数据
      await this.loadData();
      
      app.showToast('加入家庭成功', 'success');
    } catch (error) {
      console.error('加入家庭失败:', error);
      app.showToast(error.message || '加入失败，请检查邀请码');
    } finally {
      this.setData({ joinLoading: false });
    }
  },

  // 显示邀请弹窗
  async showInviteModal() {
    try {
      app.showLoading('生成邀请码...');
      
      const res = await app.request({
        url: `/families/${this.data.currentFamily.id}/invite-code`,
        method: 'POST'
      });
      
      this.setData({
        inviteCode: res.inviteCode,
        showInviteModal: true
      });
    } catch (error) {
      console.error('生成邀请码失败:', error);
      app.showToast('生成邀请码失败');
    } finally {
      app.hideLoading();
    }
  },

  // 隐藏邀请弹窗
  hideInviteModal() {
    this.setData({
      showInviteModal: false
    });
  },

  // 复制邀请码
  copyInviteCode() {
    wx.setClipboardData({
      data: this.data.inviteCode,
      success: () => {
        app.showToast('邀请码已复制', 'success');
      }
    });
  },

  // 分享邀请码
  shareInviteCode() {
    const { inviteCode, currentFamily } = this.data;
    
    wx.showShareMenu({
      withShareTicket: true,
      menus: ['shareAppMessage', 'shareTimeline']
    });
    
    // 这里可以实现分享逻辑
    app.showToast('请通过复制邀请码的方式分享');
  },

  // 显示家庭操作
  showFamilyActions() {
    this.setData({
      showFamilyActions: true
    });
  },

  // 隐藏家庭操作
  hideFamilyActions() {
    this.setData({
      showFamilyActions: false
    });
  },

  // 编辑家庭
  editFamily() {
    this.hideFamilyActions();
    wx.navigateTo({
      url: `/pages/edit-family/edit-family?id=${this.data.currentFamily.id}`
    });
  },

  // 预算管理
  manageBudget() {
    this.hideFamilyActions();
    wx.navigateTo({
      url: '/pages/budget/budget'
    });
  },

  // 成员管理
  manageMembers() {
    this.hideFamilyActions();
    wx.navigateTo({
      url: `/pages/family-members/family-members?familyId=${this.data.currentFamily.id}`
    });
  },

  // 退出家庭
  async leaveFamily() {
    const confirmed = await app.showModal('确认退出', '确定要退出当前家庭吗？退出后将无法查看家庭账单。');
    if (!confirmed) return;
    
    this.hideFamilyActions();
    
    try {
      app.showLoading('退出中...');
      
      await app.request({
        url: `/families/${this.data.currentFamily.id}/leave`,
        method: 'POST'
      });
      
      // 清除当前家庭
      app.globalData.currentFamily = null;
      wx.removeStorageSync('currentFamily');
      
      this.setData({
        currentFamily: null,
        familyMembers: [],
        budgetSummary: null
      });
      
      // 重新加载家庭列表
      await this.loadMyFamilies();
      
      app.showToast('已退出家庭', 'success');
    } catch (error) {
      console.error('退出家庭失败:', error);
      app.showToast('退出失败，请重试');
    } finally {
      app.hideLoading();
    }
  },

  // 删除家庭（仅管理员）
  async deleteFamily() {
    const confirmed = await app.showModal('删除家庭', '删除家庭后将无法恢复，所有成员将无法再访问该家庭的账单和预算，是否继续？');
    if (!confirmed) return;

    this.hideFamilyActions();

    try {
      app.showLoading('删除中...');

      await app.request({
        url: `/families/${this.data.currentFamily.id}`,
        method: 'DELETE'
      });

      app.globalData.currentFamily = null;
      wx.removeStorageSync('currentFamily');

      this.setData({
        currentFamily: null,
        familyMembers: [],
        budgetSummary: null
      });

      await this.loadMyFamilies();

      app.showToast('家庭已删除', 'success');
    } catch (error) {
      console.error('删除家庭失败:', error);
      app.showToast(error.message || '删除失败，请重试');
    } finally {
      app.hideLoading();
    }
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadData().finally(() => {
      wx.stopPullDownRefresh();
    });
  },
  
  // 头像加载失败处理
  onAvatarLoadError(e) {
    const { index } = e.currentTarget.dataset;
    const updatePath = index !== undefined 
      ? `familyMembers[${index}].avatarDisplayUrl`
      : 'userInfo.avatarDisplayUrl';
      
    this.setData({
      [updatePath]: '/images/user.png'
    });
  }
});
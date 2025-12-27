// pages/categories/categories.js
const app = getApp();

Page({
  data: {
    categoryType: 2, // 1: 收入, 2: 支出
    systemCategories: [],
    customCategories: [],
    
    // 弹窗状态
    showAddCategoryModal: false,
    showEditCategoryModal: false,
    showCategoryActions: false,
    
    // 表单数据
    categoryForm: {
      name: '',
      icon: '',
      color: '#4CAF50'
    },
    editForm: {
      id: null,
      name: '',
      icon: '',
      color: '#4CAF50'
    },
    
    // 当前操作的分类
    currentCategory: null,
    
    // 颜色选项
    colorOptions: [
      '#4CAF50', '#2196F3', '#FF9800', '#f44336', 
      '#9C27B0', '#607D8B', '#795548', '#E91E63',
      '#3F51B5', '#009688', '#8BC34A', '#CDDC39',
      '#FFC107', '#FF5722', '#9E9E9E', '#673AB7'
    ],
    
    // 加载状态
    addLoading: false,
    editLoading: false
  },

  onLoad() {
    this.loadCategories();
  },

  onShow() {
    if (!app.globalData.token) {
      wx.reLaunch({
        url: '/pages/login/login'
      });
      return;
    }
  },

  // 加载分类数据
  async loadCategories() {
    try {
      const res = await app.request({
        url: '/categories'
      });
      
      this.filterCategories(res.data || []);
    } catch (error) {
      console.error('加载分类失败:', error);
      app.showToast('加载分类失败');
    }
  },

  // 筛选分类
  filterCategories(categories) {
    const { categoryType } = this.data;
    
    const systemCategories = categories.filter(cat => 
      cat.type === categoryType && cat.isSystem
    );
    
    const customCategories = categories.filter(cat => 
      cat.type === categoryType && !cat.isSystem
    );
    
    this.setData({
      systemCategories,
      customCategories
    });
  },

  // 切换分类类型
  switchType(e) {
    const type = parseInt(e.currentTarget.dataset.type);
    this.setData({ categoryType: type });
    this.loadCategories();
  },

  // 显示添加分类弹窗
  showAddCategoryModal() {
    this.setData({
      showAddCategoryModal: true,
      categoryForm: {
        name: '',
        icon: '',
        color: '#4CAF50'
      }
    });
  },

  // 隐藏添加分类弹窗
  hideAddCategoryModal() {
    this.setData({
      showAddCategoryModal: false
    });
  },

  // 添加分类表单输入
  onCategoryNameInput(e) {
    this.setData({
      'categoryForm.name': e.detail.value
    });
  },

  onCategoryIconInput(e) {
    this.setData({
      'categoryForm.icon': e.detail.value
    });
  },

  // 选择颜色
  selectColor(e) {
    const color = e.currentTarget.dataset.color;
    this.setData({
      'categoryForm.color': color
    });
  },

  // 添加分类
  async addCategory() {
    const { categoryForm, categoryType } = this.data;
    
    if (!categoryForm.name.trim()) {
      app.showToast('请输入分类名称');
      return;
    }
    
    this.setData({ addLoading: true });
    
    try {
      const categoryData = {
        name: categoryForm.name.trim(),
        type: categoryType === 2 ? 'expense' : 'income',
        icon: categoryForm.icon.trim() || null,
        color: categoryForm.color
      };
      
      await app.request({
        url: '/categories',
        method: 'POST',
        data: categoryData
      });
      
      app.showToast('分类添加成功', 'success');
      this.hideAddCategoryModal();
      this.loadCategories();
    } catch (error) {
      console.error('添加分类失败:', error);
      app.showToast(error.message || '添加失败，请重试');
    } finally {
      this.setData({ addLoading: false });
    }
  },

  // 显示分类操作
  showCategoryActions(e) {
    const category = e.currentTarget.dataset.category;
    this.setData({
      currentCategory: category,
      showCategoryActions: true
    });
  },

  // 隐藏分类操作
  hideCategoryActions() {
    this.setData({
      showCategoryActions: false,
      currentCategory: null
    });
  },

  // 编辑分类
  editCategory() {
    const { currentCategory } = this.data;
    
    this.setData({
      showEditCategoryModal: true,
      showCategoryActions: false,
      editForm: {
        id: currentCategory.id,
        name: currentCategory.name,
        icon: currentCategory.icon || '',
        color: currentCategory.color
      }
    });
  },

  // 隐藏编辑分类弹窗
  hideEditCategoryModal() {
    this.setData({
      showEditCategoryModal: false
    });
  },

  // 编辑表单输入
  onEditNameInput(e) {
    this.setData({
      'editForm.name': e.detail.value
    });
  },

  onEditIconInput(e) {
    this.setData({
      'editForm.icon': e.detail.value
    });
  },

  // 选择编辑颜色
  selectEditColor(e) {
    const color = e.currentTarget.dataset.color;
    this.setData({
      'editForm.color': color
    });
  },

  // 更新分类
  async updateCategory() {
    const { editForm } = this.data;
    
    if (!editForm.name.trim()) {
      app.showToast('请输入分类名称');
      return;
    }
    
    this.setData({ editLoading: true });
    
    try {
      const updateData = {
        name: editForm.name.trim(),
        icon: editForm.icon.trim() || null,
        color: editForm.color
      };
      
      await app.request({
        url: `/categories/${editForm.id}`,
        method: 'PUT',
        data: updateData
      });
      
      app.showToast('分类更新成功', 'success');
      this.hideEditCategoryModal();
      this.loadCategories();
    } catch (error) {
      console.error('更新分类失败:', error);
      app.showToast(error.message || '更新失败，请重试');
    } finally {
      this.setData({ editLoading: false });
    }
  },

  // 删除分类
  async deleteCategory() {
    const { currentCategory } = this.data;
    
    const confirmed = await app.showModal('确认删除', `确定要删除分类"${currentCategory.name}"吗？删除后相关账单将无法正常显示。`);
    if (!confirmed) return;
    
    this.hideCategoryActions();
    
    try {
      app.showLoading('删除中...');
      
      await app.request({
        url: `/categories/${currentCategory.id}`,
        method: 'DELETE'
      });
      
      app.showToast('分类删除成功', 'success');
      this.loadCategories();
    } catch (error) {
      console.error('删除分类失败:', error);
      app.showToast(error.message || '删除失败，请重试');
    } finally {
      app.hideLoading();
    }
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadCategories().finally(() => {
      wx.stopPullDownRefresh();
    });
  }
});
// pages/statistics/statistics.js
const app = getApp();

Page({
  data: {
    // 时间选择
    timeType: 'month', // month | year
    selectedYear: new Date().getFullYear(),
    selectedMonth: new Date().getMonth() + 1,
    timeText: '',
    
    // 图表类型
    chartType: 'category', // category | trend
    statsType: 'expense', // expense | income
    
    // 数据
    overview: {
      totalIncome: '0.00',
      totalExpense: '0.00',
      balance: '0.00'
    },
    categoryStats: [],
    categoryTotal: '0.00',
    trendData: [],
    budgetData: [],
    
    // 弹窗
    showTimePicker: false,
    years: [],
    months: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
    timePickerValue: [0, 0],
    
    // 图表实例
    pieChart: null,
    lineChart: null
  },

  onLoad() {
    this.initTimePicker();
    this.updateTimeText();
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

  onReady() {
    // 初始化图表
    this.initCharts();
  },

  // 初始化时间选择器
  initTimePicker() {
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let i = currentYear - 5; i <= currentYear + 1; i++) {
      years.push(i);
    }
    
    const yearIndex = years.indexOf(this.data.selectedYear);
    const monthIndex = this.data.selectedMonth - 1;
    
    this.setData({
      years,
      timePickerValue: [yearIndex, monthIndex]
    });
  },

  // 初始化图表
  initCharts() {
    // 初始化饼图
    const pieCtx = wx.createCanvasContext('pieChart', this);
    this.setData({ pieChart: pieCtx });
    
    // 初始化折线图
    const lineCtx = wx.createCanvasContext('lineChart', this);
    this.setData({ lineChart: lineCtx });
  },

  // 加载数据
  async loadData() {
    try {
      await Promise.all([
        this.loadOverview(),
        this.loadCategoryStats(),
        this.loadTrendData(),
        this.loadBudgetData()
      ]);
      
      // 绘制图表
      this.drawCharts();
    } catch (error) {
      console.error('加载统计数据失败:', error);
    }
  },

  // 加载总览数据
  async loadOverview() {
    try {
      const { timeType, selectedYear, selectedMonth } = this.data;
      let url = '';
      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');

      if (timeType === 'month') {
        url = `/bills/statistics/monthly?year=${selectedYear}&month=${selectedMonth}`;
      } else {
        url = `/bills/statistics/yearly?year=${selectedYear}`;
      }

      if (currentFamily && currentFamily.id) {
        url += `&familyId=${currentFamily.id}`;
      }

      const res = await app.request({ url });
      
      this.setData({
        overview: {
          totalIncome: app.formatAmount(res.income || 0),
          totalExpense: app.formatAmount(res.expense || 0),
          balance: app.formatAmount((res.income || 0) - (res.expense || 0))
        }
      });
    } catch (error) {
      console.error('加载总览数据失败:', error);
    }
  },

  // 加载分类统计
  async loadCategoryStats() {
    try {
      const { timeType, selectedYear, selectedMonth, statsType } = this.data;
      const categoryType = statsType === 'expense' ? 2 : 1;
      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');

      let url = '';
      if (timeType === 'month') {
        url = `/bills/statistics/category?year=${selectedYear}&month=${selectedMonth}&categoryType=${categoryType}`;
      } else {
        url = `/bills/statistics/category?year=${selectedYear}&categoryType=${categoryType}`;
      }

      if (currentFamily && currentFamily.id) {
        url += `&familyId=${currentFamily.id}`;
      }

      const res = await app.request({ url });
      const stats = (res || []).map(item => ({
        ...item,
        amount: app.formatAmount(item.amount),
        percentage: Math.round(item.percentage * 100) / 100
      }));
      
      const total = stats.reduce((sum, item) => sum + parseFloat(item.amount), 0);
      
      this.setData({
        categoryStats: stats,
        categoryTotal: app.formatAmount(total)
      });
    } catch (error) {
      console.error('加载分类统计失败:', error);
    }
  },

  // 加载趋势数据
  async loadTrendData() {
    try {
      const { timeType, selectedYear, selectedMonth } = this.data;
      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
      let url = '';

      if (timeType === 'month') {
        url = `/bills/statistics/daily?year=${selectedYear}&month=${selectedMonth}`;
      } else {
        url = `/bills/statistics/monthly-trend?year=${selectedYear}`;
      }

      if (currentFamily && currentFamily.id) {
        url += `&familyId=${currentFamily.id}`;
      }

      const res = await app.request({ url });
      const trendData = (res || []).map(item => ({
        ...item,
        income: app.formatAmount(item.income || 0),
        expense: app.formatAmount(item.expense || 0),
        balance: app.formatAmount((item.income || 0) - (item.expense || 0))
      }));
      
      this.setData({ trendData });
    } catch (error) {
      console.error('加载趋势数据失败:', error);
    }
  },

  // 加载预算数据
  async loadBudgetData() {
    if (this.data.timeType !== 'month') {
      this.setData({ budgetData: [] });
      return;
    }
    
    try {
      const { selectedYear, selectedMonth } = this.data;
      const currentFamily = app.globalData.currentFamily || wx.getStorageSync('currentFamily');
      let url = `/budgets/summary?year=${selectedYear}&month=${selectedMonth}`;
      if (currentFamily && currentFamily.id) {
        url += `&familyId=${currentFamily.id}`;
      }

      const res = await app.request({
        url
      });
      
      const budgetData = (res.categoryBudgets || []).map(item => ({
        categoryId: item.categoryId,
        categoryName: item.categoryName || '未分类',
        icon: item.categoryIcon,
        color: item.categoryColor || '#666666',
        amount: app.formatAmount(item.budget),
        usedAmount: app.formatAmount(item.spent),
        usagePercentage: item.utilizationRate || 0,
        isOverBudget: !!item.isOverBudget
      }));

      this.setData({ budgetData });
    } catch (error) {
      console.error('加载预算数据失败:', error);
    }
  },

  // 绘制图表
  drawCharts() {
    if (this.data.chartType === 'category') {
      this.drawPieChart();
    } else {
      this.drawLineChart();
    }
  },

  // 绘制饼图
  drawPieChart() {
    const { pieChart, categoryStats } = this.data;
    if (!pieChart || categoryStats.length === 0) return;
    
    // 获取canvas尺寸
    const query = wx.createSelectorQuery();
    query.select('.pie-chart').boundingClientRect();
    query.exec((res) => {
      if (!res[0]) return;
      
      const { width, height } = res[0];
      const centerX = width / 2;
      const centerY = height / 2;
      const radius = Math.min(width, height) / 2 - 40;
      
      // 清空画布
      pieChart.clearRect(0, 0, width, height);
      
      // 计算总金额
      const total = categoryStats.reduce((sum, item) => sum + parseFloat(item.amount), 0);
      if (total === 0) return;
      
      // 绘制饼图
      let currentAngle = -Math.PI / 2; // 从顶部开始
      const colors = ['#4CAF50', '#2196F3', '#FF9800', '#f44336', '#9C27B0', '#607D8B', '#795548', '#E91E63'];
      
      categoryStats.forEach((item, index) => {
        const angle = (parseFloat(item.amount) / total) * 2 * Math.PI;
        const color = item.color || colors[index % colors.length];
        
        // 绘制扇形
        pieChart.beginPath();
        pieChart.moveTo(centerX, centerY);
        pieChart.arc(centerX, centerY, radius, currentAngle, currentAngle + angle);
        pieChart.closePath();
        pieChart.setFillStyle(color);
        pieChart.fill();
        
        // 绘制标签
        if (item.percentage >= 5) { // 只显示占比大于5%的标签
          const labelAngle = currentAngle + angle / 2;
          const labelX = centerX + Math.cos(labelAngle) * (radius * 0.7);
          const labelY = centerY + Math.sin(labelAngle) * (radius * 0.7);
          
          pieChart.setFillStyle('#ffffff');
          pieChart.setFontSize(12);
          pieChart.setTextAlign('center');
          pieChart.fillText(`${item.percentage}%`, labelX, labelY);
        }
        
        currentAngle += angle;
      });
      
      pieChart.draw();
    });
  },

  // 绘制折线图
  drawLineChart() {
    const { lineChart, trendData } = this.data;
    if (!lineChart || trendData.length === 0) return;
    
    // 获取canvas尺寸
    const query = wx.createSelectorQuery();
    query.select('.line-chart').boundingClientRect();
    query.exec((res) => {
      if (!res[0]) return;
      
      const { width, height } = res[0];
      const padding = 40;
      const chartWidth = width - padding * 2;
      const chartHeight = height - padding * 2;
      const pointCount = trendData.length;
      
      // 清空画布
      lineChart.clearRect(0, 0, width, height);
      
      // 计算数据范围
      const incomes = trendData.map(item => parseFloat(item.income));
      const expenses = trendData.map(item => parseFloat(item.expense));
      const maxValue = Math.max(...incomes, ...expenses);
      
      if (!isFinite(maxValue) || maxValue === 0) return;
      
      // 计算横坐标间距（只有一个点时，居中显示）
      const stepX = pointCount > 1 ? chartWidth / (pointCount - 1) : 0;
      const getX = (index) => {
        if (pointCount === 1) {
          return padding + chartWidth / 2;
        }
        return padding + index * stepX;
      };
      
      // 绘制坐标轴
      lineChart.setStrokeStyle('#e0e0e0');
      lineChart.setLineWidth(1);
      
      // Y轴
      lineChart.beginPath();
      lineChart.moveTo(padding, padding);
      lineChart.lineTo(padding, height - padding);
      lineChart.stroke();
      
      // X轴
      lineChart.beginPath();
      lineChart.moveTo(padding, height - padding);
      lineChart.lineTo(width - padding, height - padding);
      lineChart.stroke();
      
      // 绘制收入线
      if (pointCount > 1) {
        lineChart.setStrokeStyle('#4CAF50');
        lineChart.setLineWidth(2);
        lineChart.beginPath();
        trendData.forEach((item, index) => {
          const x = getX(index);
          const y = height - padding - (parseFloat(item.income) / maxValue) * chartHeight;
          
          if (index === 0) {
            lineChart.moveTo(x, y);
          } else {
            lineChart.lineTo(x, y);
          }
        });
        lineChart.stroke();

        // 绘制支出线
        lineChart.setStrokeStyle('#f44336');
        lineChart.setLineWidth(2);
        lineChart.beginPath();
        trendData.forEach((item, index) => {
          const x = getX(index);
          const y = height - padding - (parseFloat(item.expense) / maxValue) * chartHeight;
          
          if (index === 0) {
            lineChart.moveTo(x, y);
          } else {
            lineChart.lineTo(x, y);
          }
        });
        lineChart.stroke();
      }
      
      // 绘制数据点
      trendData.forEach((item, index) => {
        const x = getX(index);
        
        // 收入点
        const incomeY = height - padding - (parseFloat(item.income) / maxValue) * chartHeight;
        lineChart.beginPath();
        lineChart.arc(x, incomeY, 3, 0, 2 * Math.PI);
        lineChart.setFillStyle('#4CAF50');
        lineChart.fill();
        
        // 支出点
        const expenseY = height - padding - (parseFloat(item.expense) / maxValue) * chartHeight;
        lineChart.beginPath();
        lineChart.arc(x, expenseY, 3, 0, 2 * Math.PI);
        lineChart.setFillStyle('#f44336');
        lineChart.fill();
      });
      
      lineChart.draw();
    });
  },

  // 更新时间显示文本
  updateTimeText() {
    const { timeType, selectedYear, selectedMonth } = this.data;
    const now = new Date();
    const currentYear = now.getFullYear();
    const currentMonth = now.getMonth() + 1;

    let timeText = '';
    if (timeType === 'month') {
      if (selectedYear === currentYear && selectedMonth === currentMonth) {
        timeText = '本月';
      } else {
        timeText = `${selectedYear}年${selectedMonth}月`;
      }
    } else {
      if (selectedYear === currentYear) {
        timeText = '今年';
      } else {
        timeText = `${selectedYear}年`;
      }
    }

    this.setData({ timeText });
  },

  // 切换时间类型
  switchTimeType(e) {
    const type = e.currentTarget.dataset.type;
    this.setData({ timeType: type }, () => {
      this.updateTimeText();
      this.loadData();
    });
  },

  // 切换图表类型
  switchChartType(e) {
    const type = e.currentTarget.dataset.type;
    this.setData({ chartType: type });
    this.drawCharts();
  },

  // 切换统计类型
  switchStatsType(e) {
    const type = e.currentTarget.dataset.type;
    this.setData({ statsType: type });
    this.loadCategoryStats().then(() => {
      this.drawPieChart();
    });
  },

  // 显示时间选择器
  showTimePicker() {
    this.setData({ showTimePicker: true });
  },

  // 隐藏时间选择器
  hideTimePicker() {
    this.setData({ showTimePicker: false });
  },

  // 时间选择器变化
  onTimePickerChange(e) {
    this.setData({
      timePickerValue: e.detail.value
    });
  },

  // 确认时间选择
  confirmTimePicker() {
    const { years, months, timePickerValue, timeType } = this.data;
    
    if (timeType === 'month') {
      const selectedYear = years[timePickerValue[0]];
      const selectedMonth = months[timePickerValue[1]];
      
      this.setData({
        selectedYear,
        selectedMonth,
        showTimePicker: false
      }, () => {
        this.updateTimeText();
      });
    } else {
      const selectedYear = years[timePickerValue[0]];
      
      this.setData({
        selectedYear,
        showTimePicker: false
      }, () => {
        this.updateTimeText();
      });
    }
    
    this.loadData();
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadData().finally(() => {
      wx.stopPullDownRefresh();
    });
  }
});
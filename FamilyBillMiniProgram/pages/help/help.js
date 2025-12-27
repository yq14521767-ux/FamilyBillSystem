Page({
  data: {
    faqs: [
      {
        id: 1,
        question: '如何添加一条新的账单？',
        answer: '在首页点击“记支出”或“记收入”，或在账单页点击右上角添加按钮即可新增账单。',
        open: true
      },
      {
        id: 2,
        question: '如何切换不同家庭的数据？',
        answer: '在首页顶部点击当前家庭名称，或在“家庭”页选择/切换家庭。',
        open: false
      },
      {
        id: 3,
        question: '预算提醒是如何计算的？',
        answer: '系统会根据当前月份的支出和设置的预算进行对比，当使用率达到 80% 或超出预算时，会在首页和消息中心给出提醒。',
        open: false
      },
      {
        id: 4,
        question: '数据是否会丢失？',
        answer: '账单、家庭、成员等数据都会保存在服务器，只要账号存在，卸载或更换设备后重新登录即可同步。',
        open: false
      }
    ]
  },

  toggleFaq(e) {
    const id = e.currentTarget.dataset.id;
    const faqs = this.data.faqs.map(item =>
      item.id === id ? { ...item, open: !item.open } : item
    );
    this.setData({ faqs });
  }
});

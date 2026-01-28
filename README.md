# 家庭账单管理系统

一个用于**家庭记账与财务协作**的完整解决方案，包含：

- 微信小程序前端：家庭成员随时随地记账、查账
- ASP.NET Core 后端 API：账单、家庭、预算、通知等核心业务服务

> 适合个人练手、家庭自用，也可以作为完整前后端分离项目的学习案例。

---

## 功能特性

- **用户与认证**
  - 微信授权登录
  - 邮箱注册 / 登录（支持邮箱验证码）
  - JWT 身份认证与刷新 Token 机制

- **家庭与成员管理**
  - 创建 / 加入家庭
  - 家庭成员列表与角色管理
  - 当前家庭切换与多家庭支持

- **账单管理**
  - 收入 / 支出记录
  - 分类管理（饮食、交通、娱乐等）
  - 账单详情、编辑、删除
  - 支持备注、描述字段

- **预算与统计**
  - 设置家庭预算
  - 支出监控与预算提醒
  - 图表统计与趋势分析（按时间、类别等）

- **通知与消息**
  - 系统通知列表
  - 与预算、账单相关的提醒能力（可扩展）

- **其他**
  - 个人中心与资料编辑（头像、昵称、邮箱等）
  - 数据导出（预留扩展点，可导出为 Excel/CSV）

---

## 技术栈

- **前端：微信小程序（原生）**
  - 小程序页面：`pages/**`（记账、家庭、统计、个人中心等）
  - 使用微信小程序原生组件与 API
  - 全局工具与网络请求封装在 `app.js`

- **后端：ASP.NET Core 8.0**
  - Web API + REST 风格接口
  - Entity Framework Core + MySQL 持久化（Pomelo.EntityFrameworkCore.MySql）
  - 中间件：
    - 自定义 JWT 认证中间件
    - 全局异常处理中间件
    - 简单限流中间件等
  - 分层结构：
    - `Controllers/` 控制器
    - `DTOs/` 数据传输对象
    - `Data/` 上下文与种子数据
    - `Middleware/` 自定义中间件

- **其他**
  - 存储：七牛云 CDN（用户头像等静态资源）
  - 邮件服务：QQ 邮箱 SMTP
  - 身份认证：JWT + RefreshToken

---

## 目录结构

仓库主要包含两个子项目：

```text
.
├── FamilyBillMiniProgram/       # 微信小程序前端
│   ├── app.js                   # 全局配置与网络请求封装
│   ├── app.json                 # 小程序路由与窗口配置
│   ├── app.wxss                 # 全局样式
│   └── pages/                   # 业务页面
│       ├── index/               # 首页
│       ├── login/               # 登录 / 授权
│       ├── bills/               # 账单列表
│       ├── add-bill/            # 新增账单
│       ├── edit-bill/           # 编辑账单
│       ├── bill-detail/         # 账单详情
│       ├── family/              # 家庭列表 / 信息
│       ├── family-members/      # 家庭成员
│       ├── edit-family/         # 编辑家庭
│       ├── categories/          # 分类管理
│       ├── budget/              # 预算管理
│       ├── statistics/          # 统计图表
│       ├── notifications/       # 通知中心
│       ├── profile/             # 个人中心
│       ├── profile-detail/      # 个人资料编辑
│       ├── settings/            # 设置
│       ├── help/                # 使用帮助
│       └── about/               # 关于页面
│
└── FamilyBillSystem/            # 后端 ASP.NET Core API
    ├── FamilyBillSystem.sln
    └── FamilyBillSystem/
        ├── Controllers/         # 控制器（Auth、Bill、Budget、Family、Notifications 等）
        ├── DTOs/                # DTO 定义
        ├── Data/                # EF Core 上下文与种子数据
        ├── Middleware/          # 自定义中间件
        ├── Migrations/          # EF Core 迁移文件
        ├── appsettings.json     # 应用配置（数据库、JWT、邮箱、第三方服务等）
        ├── Program.cs           # 启动配置
        └── FamilyBillSystem.csproj
```

---

## 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/<your-name>/<your-repo>.git
cd <your-repo>
```

### 2. 启动后端 API（ASP.NET Core）

前置条件：

- 已安装 [.NET 8 SDK](https://dotnet.microsoft.com/)
- 使用 MySQL 数据库

运行步骤：

```bash
cd FamilyBillSystem/FamilyBillSystem

# 还原依赖
dotnet restore

# 应用数据库迁移（如未自动迁移）
dotnet ef database update

# 启动 API
dotnet run
```

默认启动地址（可在 `appsettings.json` / `launchSettings.json` 中查看与修改），例如：

- `https://localhost:5001`
- `http://localhost:5000`

### 3. 启动微信小程序前端

1. 打开 **微信开发者工具**
2. 选择「导入项目」，目录选择：

   ```text
   <your-repo>/FamilyBillMiniProgram
   ```

3. 填写你自己的 **小程序 AppID**（测试可以用体验版 AppID 或者在开发模式下使用）
4. 确认全局接口地址：

   在 `FamilyBillMiniProgram/app.js` 中有类似配置：

   ```js
   globalData: {
     baseUrl: 'https://your-api-host/api',
     ...
   }
   ```

   - 如果本地调试，请改为后端本地地址，例如：
     - `http://localhost:5000/api`
   - 如果部署到服务器，则改为对应域名（注意配置微信小程序合法域名）

5. 编译并预览小程序

---

## 配置说明

部分敏感信息不会提交到仓库，请根据实际情况修改 `appsettings.json` 或环境变量：

- **JWT 配置**
  - `Jwt:Key`
  - `Jwt:Issuer`
  - `Jwt:Audience`
  - `Jwt:AccessTokenExpireMinutes`
  - `Jwt:RefreshTokenExpireDays`

- **数据库配置**
  - 默认使用 MySQL，连接字符串在 `appsettings.json` 的 `ConnectionStrings:DefaultConnection` 中
  - 如需切换到 SQLite / SQL Server 等其他数据库，请修改：
    - 在 `Program.cs` 中将 `UseMySql` 替换为对应 Provider（如 `UseSqlite` 等）
    - 并调整连接字符串

- **邮箱（用于发送验证码/通知）**
  - QQ 邮箱 SMTP：
    - `Email:SmtpServer`
    - `Email:Port`
    - `Email:User`
    - `Email:Password`（授权码）

- **七牛云存储（头像等静态资源）**
  - `Qiniu:AccessKey`
  - `Qiniu:SecretKey`
  - `Qiniu:Bucket`
  - `Qiniu:Domain`

- **微信配置**
  - 小程序 `AppId` / `AppSecret`
  - 用于后端的微信登录 / 获取 openid 等接口

---

## 接口文档

- 开发环境下，启动后端后访问 Swagger：
  - `https://localhost:5001/swagger`
- 主要控制器（仅列出部分）：
  - `AuthController`：登录、注册、微信登录、刷新 Token 等
  - `BillController`：账单增删改查
  - `CategoryController`：分类管理
  - `FamilyController`：家庭及成员管理
  - `BudgetController`：预算配置与查询
  - `NotificationsController`：通知相关接口
  - `UsersController`：用户资料相关接口

---

## 截图示例（可选）

> 可在此处放几张小程序截图（首页、记账页、统计图表、家庭成员等）

```markdown
![首页](docs/images/home.png)
![记账页](docs/images/add-bill.png)
![统计页](docs/images/statistics.png)
```

---

## TODO / 未来规划

- [ ] 支持多货币与汇率换算
- [ ] 自定义统计报表与导出模板
- [ ] 定时任务（例如每月账单汇总邮件）

---

## 许可协议

根据实际情况选择合适的开源协议，例如：

- MIT License
- Apache-2.0

```text
Copyright (c) 2025 ...

Licensed under the MIT License.
```

---

如在使用或二次开发过程中有任何问题，欢迎提 Issue 或 PR。

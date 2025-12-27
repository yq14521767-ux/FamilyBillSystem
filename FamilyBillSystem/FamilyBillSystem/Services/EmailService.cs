using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace FamilyBillSystem.Services
{
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        // 通过依赖注入获取配置
        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        // 发送验证码邮件
        public async Task SendVerificationCodeAsync(string toEmail, string code)
        {
            // 邮件内容
            var subject = "【家庭账单管理】注册邮箱验证码";
            var body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2 style='color: #4CAF50;'>家庭账单管理系统</h2>
                    <p>你正在注册账号，验证码为：</p>
                    <p style='font-size: 24px; font-weight: bold; color: #4CAF50; letter-spacing: 5px;'>{code}</p>
                    <p style='color: #666;'>验证码有效期为 5 分钟，请勿泄露给他人。</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'/>
                    <p style='color: #999; font-size: 12px;'>如果这不是你的操作，请忽略此邮件。</p>
                </div>
            ";

            // 配置 SMTP 客户端
            using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
            {
                client.EnableSsl = true; // 启用 SSL 加密（必须）
                client.Credentials = new NetworkCredential(
                    _emailSettings.SmtpUsername,
                    _emailSettings.SmtpPassword
                );

                // 创建邮件消息
                var message = new MailMessage(
                    from: new MailAddress(_emailSettings.SmtpUsername, _emailSettings.SenderName),
                    to: new MailAddress(toEmail)
                )
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true // 允许 HTML 格式内容
                };

                // 发送邮件
                await client.SendMailAsync(message);
            }
        }

        
    }

    // 配置模型类（与 appsettings.json 对应）
    public class EmailSettings
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; }
        public string SmtpPassword { get; set; }
        public string SenderName { get; set; }
    }
}

using FamilyBillSystem.Utils;
using Microsoft.Extensions.Options;
using Qiniu.Http;
using Qiniu.Storage;
using Qiniu.Util;
using System.Text.Json;

namespace FamilyBillSystem.Services
{
    public class QiniuService
    {
        private readonly QiniuSettings _settings;
        private readonly ILogger<QiniuService> _logger;
        private readonly Mac _mac;

        public QiniuService(IOptions<QiniuSettings> settings, ILogger<QiniuService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _mac = new Mac(_settings.AccessKey, _settings.SecretKey);
        }

        /// <summary>
        /// 上传文件到七牛云
        /// </summary>
        /// <param name="fileStream">文件流</param>
        /// <param name="fileName">文件名（包含路径，如：avatars/user_123.jpg）</param>
        /// <returns>文件的完整访问URL</returns>
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            try
            {
                _logger.LogInformation($"开始上传文件到七牛云: {fileName}");

                // 生成上传凭证
                var putPolicy = new PutPolicy
                {
                    Scope = $"{_settings.Bucket}:{fileName}",
                    DeleteAfterDays = 0  // 0表示永久保存
                };
                var uploadToken = Auth.CreateUploadToken(_mac, putPolicy.ToJsonString());

                // 配置上传参数
                var config = new Config
                {
                    Zone = Zone.ZONE_CN_South,  // 华南区域
                    UseHttps = true,
                    UseCdnDomains = true,
                    ChunkSize = ChunkUnit.U512K
                };

                // 创建表单上传对象
                var formUploader = new FormUploader(config);

                // 将Stream转换为byte[]
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                // 上传文件
                var result = formUploader.UploadData(fileBytes, fileName, uploadToken, null);

                if (result.Code == 200)
                {
                    // 优先使用七牛返回的 key，避免与实际存储 key 不一致
                    var key = fileName;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(result.Text))
                        {
                            var uploadResult = JsonSerializer.Deserialize<QiniuUploadResult>(result.Text);
                            if (!string.IsNullOrWhiteSpace(uploadResult?.Key))
                            {
                                key = uploadResult.Key;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"解析七牛上传结果失败: {result.Text}");
                    }

                    var domain = _settings.Domain.TrimEnd('/');
                    // 确保域名包含协议前缀
                    if (!domain.StartsWith("http://") && !domain.StartsWith("https://"))
                    {
                        domain = $"https://{domain}";
                    }
                    var fileUrl = $"{domain}/{key}";
                    _logger.LogInformation($"文件上传成功: {fileUrl}");
                    return fileUrl;
                }
                else
                {
                    _logger.LogError($"七牛云上传失败: Code={result.Code}, Error={result.Text}");
                    throw new Exception($"上传失败: {result.Text}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"上传文件到七牛云时发生异常: {fileName}");
                throw;
            }
        }

        /// <summary>
        /// 删除七牛云上的文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        public async Task<bool> DeleteFileAsync(string fileName)
        {
            try
            {
                _logger.LogInformation($"开始删除七牛云文件: {fileName}");

                var config = new Config
                {
                    Zone = Zone.ZONE_CN_South,  // 华南区域（z2）
                    UseHttps = true
                };

                var bucketManager = new BucketManager(_mac, config);
                var result = bucketManager.Delete(_settings.Bucket, fileName);

                if (result.Code == 200)
                {
                    _logger.LogInformation($"文件删除成功: {fileName}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"文件删除失败: Code={result.Code}, Error={result.Text}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除七牛云文件时发生异常: {fileName}");
                return false;
            }
        }

        /// <summary>
        /// 从URL中提取文件名
        /// </summary>
        public string ExtractFileNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            // 如果不包含七牛云域名，说明不是七牛云文件，返回空
            if (!url.Contains(_settings.Domain))
                return string.Empty;

            try
            {
                // 移除可能的时间戳参数
                var cleanUrl = url.Split('?')[0];
                var uri = new Uri(cleanUrl);
                return uri.AbsolutePath.TrimStart('/');
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning($"无法解析URL格式: {url}, 错误: {ex.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"提取文件名时发生异常: {url}");
                return string.Empty;
            }
        }

        private class QiniuUploadResult
        {
            public string? Key { get; set; }
            public string? Hash { get; set; }
        }
    }
}

using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace FamilyBillSystem.Utils
{
    public class PasswordHelper
    {
        //生成密码哈希+盐
        public static void CreatePWHash(string pw,out byte[] pwHash,out byte[] pwSalt)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                //随机生成16字节盐
                pwSalt = new byte[16];
                rng.GetBytes(pwSalt);
            }

            //Argon2id
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(pw))
            {
                Salt = pwSalt,
                DegreeOfParallelism = 4,  //p=4,同时占用 4 条线程
                MemorySize = 8192,  //m=8MB,单次计算占用的内存量
                Iterations = 3  //t=3,时间迭代次数
            };

            pwHash = argon2.GetBytes(32);
        }

        //验证密码
        public static bool VerifyPW(string pw, byte[] pwHash, byte[] pwSalt)
        {
                var argon2 = new Argon2id(Encoding.UTF8.GetBytes(pw))
                {
                    Salt = pwSalt,
                    DegreeOfParallelism = 4,
                    MemorySize = 8192,
                    Iterations = 3
                };

            byte[] inputHash = argon2.GetBytes(32); //使用相同参数再次计算哈希
            return inputHash.SequenceEqual(pwHash); //逐字节对比
        }

    }
}

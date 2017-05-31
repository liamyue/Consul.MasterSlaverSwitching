using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Consul.MasterSlaverSwitching.Utils
{
    public static class StringMd5Extension
    {
        public static string CreateMd5(this string inputValue)
        {
            using (MD5 md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(inputValue));
                var strResult = BitConverter.ToString(result);
                return strResult.Replace("-", "");
            }
        }
    }
}

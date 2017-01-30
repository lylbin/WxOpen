﻿/*----------------------------------------------------------------
    Copyright (C) 2017 Senparc
    
    文件名：EncryptHelper.cs
    文件功能描述：加密、解密处理类
    
    
    创建标识：Senparc - 20170130
----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Senparc.Weixin.Exceptions;
using Senparc.Weixin.Helpers;
using Senparc.Weixin.WxOpen.Containers;
using Senparc.Weixin.WxOpen.Entities;

namespace Senparc.Weixin.WxOpen.Helpers
{
    /// <summary>
    /// EncryptHelper
    /// </summary>
    public static class EncryptHelper
    {
        ///// <summary>
        ///// SHA1加密
        ///// </summary>
        ///// <param name="str"></param>
        ///// <returns></returns>
        //public static string EncryptToSHA1(string str)
        //{
        //    SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
        //    byte[] str1 = Encoding.UTF8.GetBytes(str);
        //    byte[] str2 = sha1.ComputeHash(str1);
        //    sha1.Clear();
        //    (sha1 as IDisposable).Dispose();
        //    return Convert.ToBase64String(str2);
        //}

        #region 签名


        /// <summary>
        /// 获得签名
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="sessionKey"></param>
        /// <returns></returns>
        public static string GetSignature(string rawData, string sessionKey)
        {
            var signature = Senparc.Weixin.Helpers.EncryptHelper.SHA1_Encrypt(rawData + sessionKey);
            return signature;
        }

        /// <summary>
        /// 比较签名是否正确
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="rawData"></param>
        /// <param name="compareSignature"></param>
        /// <exception cref="WxOpenException">当SessionId或SessionKey无效时抛出异常</exception>
        /// <returns></returns>
        public static bool CheckSignature(string sessionId, string rawData, string compareSignature)
        {
            var sessionBag = SessionContainer.GetSession(sessionId);
            if (sessionBag == null)
            {
                throw new WxOpenException("SessionId无效");
            }

            if (string.IsNullOrEmpty(sessionBag.SessionKey))
            {
                throw new WxOpenException("SessionKey无效");
            }

            var signature = GetSignature(rawData, sessionBag.SessionKey);
            return signature == compareSignature;
        }

        #endregion

        #region 解密

        private static byte[] AES_Decrypt(String Input, byte[] Iv, byte[] Key)
        {
            RijndaelManaged aes = new RijndaelManaged();
            aes.KeySize = 128;//原始：256
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Key;
            aes.IV = Iv;
            var decrypt = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] xBuff = null;
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, decrypt, CryptoStreamMode.Write))
                {
                    //        cs.Read(decryptBytes, 0, decryptBytes.Length);
                    //        cs.Close();
                    //        ms.Close();

                    byte[] xXml = Convert.FromBase64String(Input);
                    byte[] msg = new byte[xXml.Length + 32 - xXml.Length % 32];
                    Array.Copy(xXml, msg, xXml.Length);
                    cs.Write(xXml, 0, xXml.Length);
                }
                xBuff = decode2(ms.ToArray());
            }
            return xBuff;
        }

        private static byte[] decode2(byte[] decrypted)
        {
            int pad = (int)decrypted[decrypted.Length - 1];
            if (pad < 1 || pad > 32)
            {
                pad = 0;
            }
            byte[] res = new byte[decrypted.Length - pad];
            Array.Copy(decrypted, 0, res, 0, decrypted.Length - pad);
            return res;
        }

        /// <summary>
        /// 解密消息
        /// </summary>
        /// <param name="sessionKey"></param>
        /// <param name="encryptedData"></param>
        /// <param name="iv"></param>
        /// <returns></returns>
        public static string DecodeEncryptedData(string sessionKey, string encryptedData, string iv)
        {
            var aesCipher = Convert.FromBase64String(encryptedData);
            var aesKey = Convert.FromBase64String(sessionKey);
            var aesIV = Convert.FromBase64String(iv);

            var result = AES_Decrypt(encryptedData, aesIV, aesKey);
            var resultStr = Encoding.UTF8.GetString(result);
            return resultStr;
        }

        /// <summary>
        /// 解密消息（通过SessionId获取）
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="encryptedData"></param>
        /// <param name="iv"></param>
        /// <exception cref="WxOpenException">当SessionId或SessionKey无效时抛出异常</exception>
        /// <returns></returns>
        public static string DecodeEncryptedDataBySessionId(string sessionId, string encryptedData, string iv)
        {
            var sessionBag = SessionContainer.GetSession(sessionId);
            if (sessionBag == null)
            {
                throw new WxOpenException("SessionId无效");
            }

            if (string.IsNullOrEmpty(sessionBag.SessionKey))
            {
                throw new WxOpenException("SessionKey无效");
            }

            var resultStr = DecodeEncryptedData(sessionBag.SessionKey, encryptedData, iv);
            return resultStr;
        }

        /// <summary>
        /// 解密UserInfo消息（通过SessionId获取）
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="encryptedData"></param>
        /// <param name="iv"></param>
        /// <exception cref="WxOpenException">当SessionId或SessionKey无效时抛出异常</exception>
        /// <returns></returns>
        public static DecodedUserInfo DecodeUserInfoBySessionId(string sessionId, string encryptedData, string iv)
        {
            var jsonStr = DecodeEncryptedDataBySessionId(sessionId, encryptedData, iv);
            JavaScriptSerializer js = new JavaScriptSerializer();
            var userInfo = js.Deserialize<DecodedUserInfo>(jsonStr);
            return userInfo;
        }

        /// <summary>
        /// 检查解密消息水印
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="appId"></param>
        /// <returns>entity为null时也会返回false</returns>
        public static bool CheckWatermark(this DecodeEntityBase entity, string appId)
        {
            if (entity == null)
            {
                return false;
            }
            return entity.watermark.appid == appId;
        }

        #endregion
    }
}

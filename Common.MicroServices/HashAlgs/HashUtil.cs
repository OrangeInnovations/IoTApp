﻿using System;
using System.Security.Cryptography;
using System.Text;

namespace Common.MicroServices.HashAlgs
{
    public class HashUtil
    {
        public static long GetLongHashCode(string stringInput)
        {
            byte[] byteContents = Encoding.Unicode.GetBytes(stringInput);
            MD5CryptoServiceProvider hash = new MD5CryptoServiceProvider();
            byte[] hashText = hash.ComputeHash(byteContents);
            return BitConverter.ToInt64(hashText, 0) ^ BitConverter.ToInt64(hashText, 7);
        }

        public static int GetIntHashCode(string stringInput)
        {
            return (int)GetLongHashCode(stringInput);
        }
    }
}
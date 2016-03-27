﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;
using OutOfMemory;
using PetaPoco;
using User = FineMIS.SYS_USER;

namespace FineMIS
{
    public sealed class Security
    {
        static Security()
        {

        }

        /// <summary>
        /// Handles the AuthenticateRequest event of the context control.
        /// </summary>
        public static void AuthenticateRequest()
        {
            // default to an empty/unauthenticated user to assign to context.User.
            var identity = new CustomIdentity(string.Empty, 0, 0, 0, false);
            var principal = new CustomPrincipal(identity);

            var context = HttpContext.Current;

            var authCookie = context.Request.Cookies[FormsAuthCookieName];
            if (authCookie != null)
            {
                FormsAuthenticationTicket authTicket;
                try
                {
                    authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                }
                catch (Exception)
                {
                    context.Request.Cookies.Remove(FormsAuthCookieName);
                    authTicket = null;

                    // log here
                }

                if (!string.IsNullOrWhiteSpace(authTicket?.UserData))
                {
                    var datas = authTicket.UserData.Split('|');
                    if (datas.Length == 4)
                    {
                        identity = new CustomIdentity(datas[0], datas[1].ToInt64(), datas[2].ToInt64(), datas[3].ToInt64(), true);
                        principal = new CustomPrincipal(identity);
                    }
                }
            }
            context.User = principal;
        }

        /// <summary>
        /// Name of the Forms authentication cookie for the current blog instance.
        /// </summary>
        public static string FormsAuthCookieName => FormsAuthentication.FormsCookieName;

        /// <summary>
        /// Signs out user out of the current blog instance.
        /// </summary>
        public static void SignOut()
        {
            // using a custom cookie name based on the current blog instance.
            var cookie = HttpContext.Current.Request.Cookies[FormsAuthCookieName];
            if (cookie != null)
            {
                cookie.Expires = DateTime.Now.AddYears(-3);
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
        }

        /// <summary>
        /// Attempts to sign the user into the current blog instance.
        /// </summary>
        /// <param name="username">The user's username.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="rememberMe">Whether or not to persist the user's sign-in state.</param>
        /// <returns>True if the user is successfully authenticated and signed in; false otherwise.</returns>
        public static bool AuthenticateUser(string username, string password, bool rememberMe)
        {
            var un = (username ?? string.Empty).Trim();
            var pw = (password ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(un) && !string.IsNullOrWhiteSpace(pw))
            {
                var user = User.SingleOrDefault(Sql.Builder.Where("UserName = @0", un).Where("Active = @0", true));

                if (user != null && user.Password == pw)
                {
                    var context = HttpContext.Current;
                    var expirationDate = DateTime.Now.Add(FormsAuthentication.Timeout);

                    var ticket = new FormsAuthenticationTicket(
                        1,
                        un,
                        DateTime.Now,
                        expirationDate,
                        rememberMe,
                        $"{user.Name}|{user.RoleId}|{user.Id}|{user.CmpyBelongTo}",
                        FormsAuthentication.FormsCookiePath
                        );

                    var encryptedTicket = FormsAuthentication.Encrypt(ticket);

                    // setting a custom cookie name based on the current blog instance.
                    // if !rememberMe, set expires to DateTime.MinValue which makes the
                    // cookie a browser-session cookie expiring when the browser is closed.
                    var cookie = new HttpCookie(FormsAuthCookieName, encryptedTicket)
                    {
                        Expires = rememberMe ? expirationDate : DateTime.MinValue,
                        HttpOnly = true
                    };

                    context.Response.Cookies.Add(cookie);

                    return true;
                }
            }

            return false;
        }

        #region Utilities

        /// <summary>
        /// Encrypts a string using the SHA256 algorithm.
        /// </summary>
        /// <param name="plainMessage">
        /// The plain Message.
        /// </param>
        /// <returns>
        /// The hash password.
        /// </returns>
        public static string HashPassword(string plainMessage)
        {
            var data = Encoding.UTF8.GetBytes(plainMessage);
            using (HashAlgorithm sha = new SHA256Managed())
            {
                sha.TransformFinalBlock(data, 0, data.Length);
                return Convert.ToBase64String(sha.Hash);
            }
        }

        /// <summary>
        /// Generates random password for password reset
        /// </summary>
        /// <returns>
        /// Random password
        /// </returns>
        public static string RandomPassword()
        {
            var chars = "abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            var password = string.Empty;
            var random = new Random();

            for (var i = 0; i < 8; i++)
            {
                var x = random.Next(1, chars.Length);
                if (!password.Contains(chars.GetValue(x).ToString()))
                {
                    password += chars.GetValue(x);
                }
                else
                {
                    i--;
                }
            }

            return password;
        }

        #endregion
    }
}
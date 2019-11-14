﻿//******************************************************************************
// <copyright file="license.md" company="RawCMS project  (https://github.com/arduosoft/RawCMS)">
// Copyright (c) 2019 RawCMS project  (https://github.com/arduosoft/RawCMS)
// RawCMS project is released under GPL3 terms, see LICENSE file on repository root at  https://github.com/arduosoft/RawCMS .
// </copyright>
// <author>Daniele Fontani, Emanuele Bucarelli, Francesco Mina'</author>
// <autogenerated>true</autogenerated>
//******************************************************************************
using Newtonsoft.Json.Linq;
using RawCMS.Library.Core;

namespace RawCMS.Plugins.Core.Lambdas
{
    public class UserInfoLambda : RestLambda
    {
        public override string Name => "UserInfo";

        public override string Description => "UserInfo";

        public override JObject Rest(JObject input)
        {
            JObject jj = new JObject
            {
                ["IsAuthenticated"] = Request.User.Identity.IsAuthenticated
            };
            foreach (System.Security.Claims.Claim claim in Request.User.Claims)
            {
                int suffix = 0;
                string uniquekey = claim.Type;

                while (jj.ContainsKey(uniquekey))
                {
                    suffix++;
                    uniquekey = claim.Type + "[" + suffix + "]";
                }

                jj[uniquekey] = claim.Value;
            }
            return jj;
        }
    }
}
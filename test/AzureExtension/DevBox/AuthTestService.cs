// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http.Headers;
using AzureExtension.Contracts;
using Microsoft.Extensions.Hosting;

// using Azure.Core;
// using Azure.Identity;
namespace AzureExtension.Test.DevBox;

internal class AuthTestService : IDevBoxAuthService
{
    private readonly IHost _host;

    public AuthTestService(IHost host)
    {
        _host = host;
    }

    private static string GetAccessToken(bool forControlPlane = false, bool useCache = true)
    {
        // var credential = new DefaultAzureCredential();
        if (useCache)
        {
            var managementToken = "eyJ0eXAiOiJKV1QiLCJyaCI6IjAuQWhvQXY0ajVjdkdHcjBHUnF5MTgwQkhiUjBaSWYza0F1dGRQdWtQYXdmajJNQk1hQVBBLiIsImFsZyI6IlJTMjU2IiwieDV0IjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIiwia2lkIjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIn0.eyJhdWQiOiJodHRwczovL21hbmFnZW1lbnQuYXp1cmUuY29tLyIsImlzcyI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0LzcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0Ny8iLCJpYXQiOjE3MDAyNjE2NDIsIm5iZiI6MTcwMDI2MTY0MiwiZXhwIjoxNzAwMjY2MzMwLCJfY2xhaW1fbmFtZXMiOnsiZ3JvdXBzIjoic3JjMSJ9LCJfY2xhaW1fc291cmNlcyI6eyJzcmMxIjp7ImVuZHBvaW50IjoiaHR0cHM6Ly9ncmFwaC53aW5kb3dzLm5ldC83MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDcvdXNlcnMvMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwL2dldE1lbWJlck9iamVjdHMifX0sImFjciI6IjEiLCJhaW8iOiJBVlFBcS84VkFBQUFDamdhM0c2RkRlejVmWmsvUzNKS2V2aE5KQ3hCK1I5VlRsSGNweE4wZndUTjhsaWRwNHNLcnhudDZIRzhTZjJFZHlZeDlNK2c4ZWQxajZoTnhDNVo5L1RFWnJuUHpzQUlxSEVjeHFSWTFmdz0iLCJhbXIiOlsicnNhIiwibWZhIl0sImFwcGlkIjoiODcyY2Q5ZmEtZDMxZi00NWUwLTllYWItNmU0NjBhMDJkMWYxIiwiYXBwaWRhY3IiOiIwIiwiZGV2aWNlaWQiOiIyMDZkNzZiYi01YjVjLTQ2MTEtOTcwNS04Yjk0NDE5MDIyZGQiLCJmYW1pbHlfbmFtZSI6IkRhbmlzaCIsImdpdmVuX25hbWUiOiJIdXphaWZhIiwiaWR0eXAiOiJ1c2VyIiwiaXBhZGRyIjoiMjAwMTo0ODk4OjgwZTg6Mzc6MjExMTpmMzA0OjMxNzI6YTRiMyIsIm5hbWUiOiJIdXphaWZhIERhbmlzaCIsIm9pZCI6IjI4YWViYmE4LTk5NmItNDM0Yi1iYjU3LTQ5NjljMDk3YzhmMCIsIm9ucHJlbV9zaWQiOiJTLTEtNS0yMS0yMTI3NTIxMTg0LTE2MDQwMTI5MjAtMTg4NzkyNzUyNy00MTE4OTQwNCIsInB1aWQiOiIxMDAzMjAwMDlENUM1MERFIiwicmgiOiJJIiwic2NwIjoidXNlcl9pbXBlcnNvbmF0aW9uIiwic3ViIjoiSGxkbEw2MVhUSGhaeU1jYWxrd0xtNXJhSTJrbjBDTV93bTRQdUtQOS1mYyIsInRpZCI6IjcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0NyIsInVuaXF1ZV9uYW1lIjoibW9kYW5pc2hAbWljcm9zb2Z0LmNvbSIsInVwbiI6Im1vZGFuaXNoQG1pY3Jvc29mdC5jb20iLCJ1dGkiOiJ1SmRtTDFTT25FLUpCQ2ZhcUMwR0FBIiwidmVyIjoiMS4wIiwid2lkcyI6WyJiNzlmYmY0ZC0zZWY5LTQ2ODktODE0My03NmIxOTRlODU1MDkiXSwieG1zX2NhZSI6IjEiLCJ4bXNfdGNkdCI6MTI4OTI0MTU0N30.dXp7UNvMxd1VP9qJWVwptopsAbsE4xrmRx9NlnBTHV_wXb4A9E3_bzz1dQdtsjQPfVztcXFJroMAWQsj0TPej6kImZy1cSatYGQA8rrSuiixCO7z6gjKdnqAmvQUciKN-6CjUgDHBX9q5p3UlAiHJ2ExWEOEC6DaZ-sf3zTv88Bpy_pV5kYC9kr0Bib3woZY05fKREPAkAE_jzOfvU9LMHp0o9wV8OLEM-ziTcQ7Yp6QahQ7wEmLqS0v1s5UgaNM9BMbDnGkA-ds9W8tQrtRHjkI6L8eLOq8Cr0uzm7InDQf0ltNTa6sDc1MUI1O-xZprEQK_ORValxwDyB5tkORnw";
            var controlToken = "eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJ4NXQiOiJIYmpNWjFDU2VFSkRLQUdkSERDOVYtZFBRZEUiLCJ6aXAiOiJERUYifQ.uytxpxpqsa07k1w1axPcNsirkdiuC8L54Bi1qmjSBMdg1CjXSVGjT-0mdHmlrZaj2DsWfJuYDtnUW0GUNdA7O0bMNGzjmbJrKbGNkmPAbPosbJ4-IAqXkzQdJc945CFb-nf0NCRXzZJEQrSRmAowYOCpQX7Bm08A_apus8iZWFhjyaPO2c8_1460e1L-ZHz3C7ws_BsvfWma28oVNYCVl9XZtB5KI0HBd_tMt3KkTEDLZBHC_RvmsJv3yN07BvkYfJlNgvH0Xl0x3MhoELEugDqe7UoibA7nl64DS7UocVZPxxKZX7ic56duSwmGOW7WG44MgYvkNArxF8TkjgW6UA.uNFwEUH2zRln2zpqG_V_3g.WvMSsduOG-MV7a7UPTbvz6X2gbQlIdRq5K5IoNRsAE_9r4oUR61FBtIw07aAVHXXcksMx5TuW8YFuRQQnd_hTabYPPmZFerwBipDG5Ze33b6r8KopMNAk-cXbZ6nPAj_Tq7DVz41eWCHXjyhpjm0e7DiRoAhPQqH_ci9j9m9V8Mv6wpBKRuhzQP3zxEGpKqb3sej-XHjsC5aDx0tFNqJsZZqsBMn3ih80vGpKgMB2pWvebN9c6mxGVYktDIsyV64j_yumWOvleFSGpqyJqGHwp-3CIXUK1qXJehNbCKztBvXycTaImhOTOz7yqqLGjwhiHDfDsUIJ4WQ35x8ZfKKNDI7r9AOxnNpGwUpaDPp4Svnh3OBdfenXKHHct3j-Ls4gpUOPAPO2tRde-AZlO9RbVQxLQ0oncfyZfrLgWbecCtQA8UW2S_45S8JZt30IIMt-mBXCUavBw6WKgLQDsLuKCrunUVxUWpLUohQYHOaM49qeynjlvbqH80wKwk2I9FtPmBp_abWZ2ZUJkiyN4nwRHDFvr4LCfs0VAdsQe_lBV1ma_QHQEBRiSJ_4VAWLjB4y9TJWWqcuaQseEjlYI63STPgxfIposMtREeAaM5Vb9LsaWFzqsbhUZkkEy6b1h79bhm3RBxjLzC8sPlf2QOpTXeRHlYjpWamu0PEqohoxuC7cOusMgPd3hSlF4Trm_WV0_R_6C2LIRB6_58ZlZM0ltKtcV2UgZFjTaYk0dVDSBLnZb-dQJUtjsqe351EbdHUn1rb5uSS_nXjfsmVHH-Nsn5PmO-zcM0ExtkYLITpcud49T1yJhmVKWG474JkgrpuABUw2twGP3eN0ujpZaShYHtM0Ut66mZSsdDFbZEHQlPUfp5bD8pEggToiGadt2G87kH8Jf7l1NRfYMabDJtKyx39phEBFBLg6Y0gLZCTF4-6RBhFIJy0lbhzIXhqdwMGwRA7LW-7rvoZZIfSqH7fWkAIKIcDMTZW2Vw1D86T9K7WHnraYriO-i3TXfxpRmOl5_7Y7ZdPZ_sA08lq_Rie2-bRKZ87lNXUEP3NUaNgl0j_aBEf_8BXQVLgl94mb71iC0cashkyzTKl-cm2q-orgUG634nLFlSi9n_uLahDZfHAQ86AMWtdBYhnRF6AwYIBShZBsxNqLQKtUy4vffF9Gzdhhi6uolKb9DlfKsj-aEWY8Sc-C9p-Hov6dUDBNPHRrk_NuCTP16LBcobEqrfc-1Ica98FuVDSL0goZFYSyPWCUSsJtfAxGaja0RsZz04E8r01vol6741HQKlMBOSuTLXIa1Z3BKZlcGEyMNeDIlotq8ppT5Tv8t3ODGIK4ApXTgqpTGKQZqdFDtNfCLCTcNKxPrZR2V-w901qQvt_s7wGg1sxGaa_AeLnxDU-01vMbhSDBNeDrHtqo9X7Q87GWp0jftOHi2OGBa3vUJdDasNXRaEmx8wm16Ol6hUB_-VbmEfVHSu1BoxDqPr64FSn4o2GZSBMtsoZoTBzS_vnwpWp23qiWH9iEU0Xhy__Ykr92cry4cAKLoJRZsq3O4ic5qCQpqT4cgnO0ZyvBYwJLtUYgtUdOb7ilNFqPwc6hOO_KdFPTIBWyWrfQYCBNs8HRgHSamYJ8jxTgEZU54tDZTfGUn7GOymvXbqsVnS0v7XoGXNmeHa4dAZzcXqhFttRkt0Ma0v1qDB1vtUVy9p0eIvoMm9CMv5FHP1N-3Uay3CaxmrvvjgnyG8gjaCjzOikfc1xAy4PB6X0MlJHMT7JvDxSPoCfVzDztI3MeRbaEMh7.QWkepBoAx9VXK0trxSamNw";
            return forControlPlane ? controlToken : managementToken;
        }

        // else
        // {
        //    if (forControlPlane)
        //    {
        //        var token = credential.GetToken(new TokenRequestContext(new[] { "https://devcenter.azure.com/" }));
        //        Console.WriteLine("Control Token -");
        //        Console.WriteLine(token.Token);
        //        return token.Token;
        //    }
        //    else
        //    {
        //        var token = credential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/" }));
        //        Console.WriteLine("Management Token -");
        //        Console.WriteLine(token.Token);
        //        return token.Token;
        //    }
        // }
        return string.Empty;
    }

    public HttpClient GetDataPlaneClient()
    {
        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetAccessToken(true));
        return httpClient;
    }

    public HttpClient GetManagementClient()
    {
        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetAccessToken());
        return httpClient;
    }
}

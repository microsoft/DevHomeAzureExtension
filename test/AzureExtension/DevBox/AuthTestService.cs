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
            var managementToken = "eyJ0eXAiOiJKV1QiLCJyaCI6IjAuQWdFQXY0ajVjdkdHcjBHUnF5MTgwQkhiUjBaSWYza0F1dGRQdWtQYXdmajJNQk1hQVBBLiIsImFsZyI6IlJTMjU2IiwieDV0IjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIiwia2lkIjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIn0.eyJhdWQiOiJodHRwczovL21hbmFnZW1lbnQuYXp1cmUuY29tLyIsImlzcyI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0LzcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0Ny8iLCJpYXQiOjE3MDAyNTE4MjksIm5iZiI6MTcwMDI1MTgyOSwiZXhwIjoxNzAwMjU2NDQwLCJfY2xhaW1fbmFtZXMiOnsiZ3JvdXBzIjoic3JjMSJ9LCJfY2xhaW1fc291cmNlcyI6eyJzcmMxIjp7ImVuZHBvaW50IjoiaHR0cHM6Ly9ncmFwaC53aW5kb3dzLm5ldC83MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDcvdXNlcnMvMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwL2dldE1lbWJlck9iamVjdHMifX0sImFjciI6IjEiLCJhaW8iOiJBVlFBcS84VkFBQUFyTHQzYWd2SERCN2V1RVRLRm5qTlZ6SE5QUHRMcXlCc0VUK1poWE5LQUkyQWp1dFhGdzI2bWN5YVRzK1RjUFFyTFNoSzNMUm1yRlFGOS9NUDFQTG1IQmVkdWlVTFlESzJhZVdwc1owenVoaz0iLCJhbXIiOlsicnNhIiwibWZhIl0sImFwcGlkIjoiODcyY2Q5ZmEtZDMxZi00NWUwLTllYWItNmU0NjBhMDJkMWYxIiwiYXBwaWRhY3IiOiIwIiwiZGV2aWNlaWQiOiIyMDZkNzZiYi01YjVjLTQ2MTEtOTcwNS04Yjk0NDE5MDIyZGQiLCJmYW1pbHlfbmFtZSI6IkRhbmlzaCIsImdpdmVuX25hbWUiOiJIdXphaWZhIiwiaWR0eXAiOiJ1c2VyIiwiaXBhZGRyIjoiMjAwMTo0ODk4OjgwZTg6MToyMTQ3OmYzMDQ6MzE3MjphNGIzIiwibmFtZSI6Ikh1emFpZmEgRGFuaXNoIiwib2lkIjoiMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwIiwib25wcmVtX3NpZCI6IlMtMS01LTIxLTIxMjc1MjExODQtMTYwNDAxMjkyMC0xODg3OTI3NTI3LTQxMTg5NDA0IiwicHVpZCI6IjEwMDMyMDAwOUQ1QzUwREUiLCJyaCI6IkkiLCJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLCJzdWIiOiJIbGRsTDYxWFRIaFp5TWNhbGt3TG01cmFJMmtuMENNX3dtNFB1S1A5LWZjIiwidGlkIjoiNzJmOTg4YmYtODZmMS00MWFmLTkxYWItMmQ3Y2QwMTFkYjQ3IiwidW5pcXVlX25hbWUiOiJtb2RhbmlzaEBtaWNyb3NvZnQuY29tIiwidXBuIjoibW9kYW5pc2hAbWljcm9zb2Z0LmNvbSIsInV0aSI6InZCQTVmTEJhNWt5amNkX1ZSbWNGQUEiLCJ2ZXIiOiIxLjAiLCJ3aWRzIjpbImI3OWZiZjRkLTNlZjktNDY4OS04MTQzLTc2YjE5NGU4NTUwOSJdLCJ4bXNfY2FlIjoiMSIsInhtc190Y2R0IjoxMjg5MjQxNTQ3fQ.omserbx-tWfxuMH4jin9Dzrnuya2UADQbyAHc2HbBWGZx-J5OzekN71MexLOxuKNVcd1wUWB6nMH0Oy9_gUHS91IAJmh9vjZw5x5RDTGnFNU2yfNy1tVFuzhgDcB5v5SbCWoVhrSRyTaLM8FcHbwhZhfGr-rldtEPz4_GSOAvumKun5UNuyn8O6RnDUH_4rUoL0VHp9uYFm47QWvKmRGc7SSijrEUP9JBT_bkg38MPfxJVcn-2wakfGepzmjPDG9X86PqvLNinYOj-shsMYd3Hir4nTwT8E_xV6VLM19YuI0G4ZN8gaKASiOQBaNnAzdL3CGd02rTa2G_5IXuHM_fg";
            var controlToken = "eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJ4NXQiOiJIYmpNWjFDU2VFSkRLQUdkSERDOVYtZFBRZEUiLCJ6aXAiOiJERUYifQ.by71sSC-2jfri9XWIUjdfxLgJa50gKZaSn1eW8b8ykt3xSAeyE2EJ2gwjZxTcaorMh2ZkqVBAVRA3a2Vtzx9NZuhwmodYEHnNBmxC1rloGElZOLqZiOgxsOMChbTXo4ENrfSn5EPEu6r7aut3ODNV8O1v-h211VZd8a1m-P7e5JPKQS3-SjZSkW2Bbqt-KAzc9D9CSXUVNJSW6SMnbXg_75oSeN97rl6inEntlF8VlIlh2t3hg_9VvcYlTrui8E5eNITin_lh3whP0MMQPVVSWTXteLhGi3iuCXj5hBxh0oXLFQTYlFg82Ihk9qbiT2leg031zEZorGuM9qnMQqb8Q.VFq9h3hqqnBElCGqdcDeGQ.LlkEAQh6jYiUMrc4_qspqu5YucWTAnVV4gELW7KgeBwkWrluac-rstRPEn8iV-SjJlrKWJa-o5OApRb4gKyWcQ4bvSHBKICEoqK8osuZHuNIBYH0GCDQGLAOZOZSTIMirWzkFxDIGF8k0o2Seos1BymerDQfgdFLOiCb3nR15A7glxwHsCuwDnmT8MWiF4vKp4Zz3peIc6twzFyShGwi55g2y3h8kMe-36N8cb_SXy75iAW3eVTBw6kZHFvDiMT2S5FIQos_UAbHKjbWFmqB-R7wztvA5p98E209rU5OC2w_blueg0zStRRiZhL8z42Fm9y8Ay2kVi1ohwtviI2126CYZMJPEKe5ZLCB_ocJGyfOUalYvMUHzKndRnnEOmz_zYNWei2fSMEanQwyQVGf20pb74jcdD4ILz9qMrNvD3xP8gA_mFnZxEO_u3G9rWSgwBRSnTkSFhCZia9cqij42BuD2vEcY8Tna0pb-BoBpz-NcxYj2A7W3fWXSYXqq-mLhQ07P1ZOmZ8fihmFa2CETSxLJ2QGbinXhmRPE7S2FQOj4qGXTbD56VdfciepLALzJ14zMqdoDoEwXaUyZoEBLiA4kCYx8PqYofnx7ntDkWkotVOAx5TwZlcU5C1G_AFxC00bSWK2Rxe-17yDFq9Q6ztht-MCyHMxkj9Z-dYzKykVVgoABESepo_w7P-9ZboyKdPRKSqkJvYfOadMLDJX-It7p1BAxw1wLL8kKCPUOUBmlIcvJslCZ5-vqY1vaigRBOZRe0y8WO9ZMFe2_Q4OjemX4kiRRQL6yRRhj-hr_fUp775YuMMxsPzuyctTpzPTA2i0-BTEHo0kecRYCrIJ5D7WYpY-VzgQ3QAc9dsxwgu6khCAOvWT-bQkPZFRE8AKAm4Sag4JazE-k9x-t0HvFY3OzFxZmygv1yGEtJnn7kKe9noaJXckMKz_dINqEOKRErrcEsKFpWqIMM4_LUQ5yUpjMpE3IgRkFEx7e8A1qCqTOkNufTomODHcKTlt8ubEnWrZm6qkV8MOZIgYWRk7Rm2Owmuj1yQrih2Ga3eMZFEnw-9gLzfoMRngq8vBWxTZ_SKCZrJwmuqxnqCRN_H0XX9Ojh_lsrePC97vrHptQZztx6rfBO_In2lKi3B3JBcThQ1qhtREG7KUnfYrKEbSjUXXAolFeiviCpCuCEhRjh5DmkgRzGRCB4URnSFwR3sy9SRJRUXg6EC0xvDZtYzeNKnxkm3N2uuM6c5ajVDvflqJeCoWxSapZa-Od9MgvlC2ALblpAb-PkH6xwxpZZGetytHqBjWatLiKtlVvV-Kiv_L4WClCTgp0U-0sCqplj4iUz4LRcS6nPJ6v2wbbz-SuXR3gr3JBJQIkRsFOiLVoxUlYWwlbaxJAqlrIfkFbBVXm1gZF4-fYPpUoroSV-G86lvxlnNrnxEeJEW5xM53a3jzi4cI0r1qg4_UeopyA5fUoF8uH8mniMMyGDYq0z0xfYq2EMog1Xk4GEtv4IqyOdTCHZsoFBoow3uTKoOud7tq0iqm7Bl5Xpxk-JTpNZ57EjYEq1IbshqtQBIqdpnoqfPYk5XFIlV5AK-l3XAgwiFAQOMLpMqOVYOPmkTCJm3A0Szjg7ZWpZUfVIi5Rvxp8hc5bORBMWdYxyxjbTlVw5vd8Xe-x_rLXGZHYIvEiJa3ppL8T4kbi20Yq7SmUL8vhcCSuafkb7XIOZLCTiqQzvpisP2yYolXrABKvwLkb_6eBn40vtL-GptSLuDQXCCNQatSkImhPaMEwsL64cPB3WhF.mUNEpya5UEivO3oM0435KA";
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

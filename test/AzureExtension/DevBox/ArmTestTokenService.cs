﻿// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;

namespace AzureExtension.Test.DevBox;

public class ArmTestTokenService : IArmTokenService
{
    public async Task<string> GetTokenAsync()
    {
        await Task.Delay(0);
        return "eyJ0eXAiOiJKV1QiLCJyaCI6IjAuQWdFQXY0ajVjdkdHcjBHUnF5MTgwQkhiUjBaSWYza0F1dGRQdWtQYXdmajJNQk1hQVBBLiIsImFsZyI6IlJTMjU2IiwieDV0IjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIiwia2lkIjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIn0.eyJhdWQiOiJodHRwczovL21hbmFnZW1lbnQuYXp1cmUuY29tLyIsImlzcyI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0LzcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0Ny8iLCJpYXQiOjE3MDAyNjY2MDUsIm5iZiI6MTcwMDI2NjYwNSwiZXhwIjoxNzAwMjcxOTE1LCJfY2xhaW1fbmFtZXMiOnsiZ3JvdXBzIjoic3JjMSJ9LCJfY2xhaW1fc291cmNlcyI6eyJzcmMxIjp7ImVuZHBvaW50IjoiaHR0cHM6Ly9ncmFwaC53aW5kb3dzLm5ldC83MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDcvdXNlcnMvMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwL2dldE1lbWJlck9iamVjdHMifX0sImFjciI6IjEiLCJhaW8iOiJBVlFBcS84VkFBQUFLQUc0TTJpbm9FNDdWS1JlN3Z2OWlaT09VWm16T2tlUEpua3JiT3NpNWdLY1N3NVJoMmNjbVJVK0VtTFdVaXFwWEV0UFdYa2FOT01pakhXMWcyNWRQUzZvQ0R3UTN1bXlqL1BuUGM5bGxuMD0iLCJhbXIiOlsicnNhIiwibWZhIl0sImFwcGlkIjoiODcyY2Q5ZmEtZDMxZi00NWUwLTllYWItNmU0NjBhMDJkMWYxIiwiYXBwaWRhY3IiOiIwIiwiZGV2aWNlaWQiOiIyMDZkNzZiYi01YjVjLTQ2MTEtOTcwNS04Yjk0NDE5MDIyZGQiLCJmYW1pbHlfbmFtZSI6IkRhbmlzaCIsImdpdmVuX25hbWUiOiJIdXphaWZhIiwiaWR0eXAiOiJ1c2VyIiwiaXBhZGRyIjoiMjAwMTo0ODk4OjgwZTg6YjoyMTNkOmYzMDQ6MzE3MjphNGIzIiwibmFtZSI6Ikh1emFpZmEgRGFuaXNoIiwib2lkIjoiMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwIiwib25wcmVtX3NpZCI6IlMtMS01LTIxLTIxMjc1MjExODQtMTYwNDAxMjkyMC0xODg3OTI3NTI3LTQxMTg5NDA0IiwicHVpZCI6IjEwMDMyMDAwOUQ1QzUwREUiLCJyaCI6IkkiLCJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLCJzdWIiOiJIbGRsTDYxWFRIaFp5TWNhbGt3TG01cmFJMmtuMENNX3dtNFB1S1A5LWZjIiwidGlkIjoiNzJmOTg4YmYtODZmMS00MWFmLTkxYWItMmQ3Y2QwMTFkYjQ3IiwidW5pcXVlX25hbWUiOiJtb2RhbmlzaEBtaWNyb3NvZnQuY29tIiwidXBuIjoibW9kYW5pc2hAbWljcm9zb2Z0LmNvbSIsInV0aSI6Ik4xWF9FOVNMZGtLVnpWeXlwVGNBQUEiLCJ2ZXIiOiIxLjAiLCJ3aWRzIjpbImI3OWZiZjRkLTNlZjktNDY4OS04MTQzLTc2YjE5NGU4NTUwOSJdLCJ4bXNfY2FlIjoiMSIsInhtc190Y2R0IjoxMjg5MjQxNTQ3fQ.dV8s299TppUuxaAKeDJAnQfIoKEDHM3ptV75bwmZHCpJkK1USlmVhjyPgdBpoNd7DeAR3RXj_VjHWDaCbIUvE6xMgTu_7iO4mPob6tRJ9jQc9XKa1VRP_hBTjsovf2Ml_ayc8kxbtXqDAiIUMPeuqIjF2p14Q3ix4X43miDgXU0u4UInLsG2FwpiWJUaoIKM-aMdhEyJzQOxUPeEtJwuZVfyIfkPmJM40U3MxPp6gacSzQoIE9sgeIO4RnJqlL0e-b2nSycXuljTQ-jAaQNHFD--MAcOMSwqi4sGW4y_CpO6ZXO-UQS5FeVm97PXV2ohZdrEQBmKqoyrEwxVTWoHdQ";
    }
}
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Serilog;
using Serilog.Configuration;

namespace DevHomeAzureExtension.Test;

public static class TestContextSinkExtensions
{
    public static LoggerConfiguration TestContextSink(
              this LoggerSinkConfiguration loggerConfiguration,
              string outputTemplate,
              TestContext? context = null,
              IFormatProvider? formatProvider = null)
    {
        return loggerConfiguration.Sink(new TestContextSink(formatProvider!, context!, outputTemplate));
    }
}

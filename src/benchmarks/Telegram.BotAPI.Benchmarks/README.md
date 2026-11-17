# Telegram.BotAPI client benchmarks

Run with `dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks/Telegram.BotAPI.Benchmarks.csproj`.

The benchmark uses a local stub HTTP handler; it measures client-side serialization, request construction, response parsing, and allocations without Telegram network latency.

## Update HTTP stream benchmark

`UpdateHttpBenchmarks` compares the original and optimized clients end-to-end:
a real loopback TCP connection, an HTTP/1.1 chunked JSON response, and
`BotResponse<IEnumerable<Update>>` deserialization.

The mock server deliberately uses `TcpListener` rather than a stub
`HttpMessageHandler` or `StringContent`. This exercises the actual
`HttpClient` transport response stream. The original client uses its normal
buffering behavior; the optimized client requests `ResponseHeadersRead` and
deserializes from the response stream.

Run only this benchmark with:

```bash
dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks -- --filter '*UpdateHttpBenchmarks*'
```

The server runs on loopback and does not contact Telegram. Benchmark numbers
include local TCP/HTTP overhead, so compare original and optimized results from
the same run; they are not measurements of Telegram network latency.

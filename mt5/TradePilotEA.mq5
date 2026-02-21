#property strict
#property description "TradePilot EA - snapshot sender for connector ingestion"

input string InpSourceId        = "demo-source-01";
input string InpSharedSecret    = "replace-with-ea-secret";
input string InpConnectorUrl    = "http://127.0.0.1:5138/ingest/snapshot";
input int    InpTimerSeconds    = 2;
input int    InpTimeoutMs       = 5000;
input int    InpMaxRetries      = 3;
input int    InpRetryDelayMs    = 250;

int OnInit()
{
   if(InpTimerSeconds < 1)
   {
      Print("TradePilotEA: InpTimerSeconds must be >= 1.");
      return INIT_PARAMETERS_INCORRECT;
   }

   if(StringLen(InpSourceId) == 0 || StringLen(InpSharedSecret) == 0 || StringLen(InpConnectorUrl) == 0)
   {
      Print("TradePilotEA: SourceId, SharedSecret, and ConnectorUrl are required.");
      return INIT_PARAMETERS_INCORRECT;
   }

   MathSrand((int)(GetTickCount() ^ TimeLocal()));
   EventSetTimer(InpTimerSeconds);

   Print("TradePilotEA initialized. SourceId=", InpSourceId, ", Connector=", InpConnectorUrl, ", Interval=", InpTimerSeconds, "s");
   return INIT_SUCCEEDED;
}

void OnDeinit(const int reason)
{
   EventKillTimer();
   Print("TradePilotEA stopped. reason=", reason);
}

void OnTick()
{
   // Snapshot flow runs on timer to maintain a stable 1-2 second cadence.
}

void OnTimer()
{
   string json = BuildSnapshotJson();
   if(StringLen(json) == 0)
   {
      Print("TradePilotEA: Failed to build snapshot JSON.");
      return;
   }

   bool sent = SendSnapshotWithRetry(json);
   if(!sent)
   {
      Print("TradePilotEA: Snapshot send failed after retries.");
   }
}

bool SendSnapshotWithRetry(const string body)
{
   int attempts = MathMax(1, InpMaxRetries);
   for(int attempt = 1; attempt <= attempts; attempt++)
   {
      if(SendSnapshot(body, attempt))
      {
         return true;
      }

      if(attempt < attempts && InpRetryDelayMs > 0)
      {
         Sleep(InpRetryDelayMs);
      }
   }

   return false;
}

bool SendSnapshot(const string body, const int attempt)
{
   long timestamp = (long)TimeGMT();
   string timestampText = IntegerToString((int)timestamp);
   string nonce = GenerateNonce();

   string payload = timestampText + "." + nonce + "." + body;
   string signature = ComputeHmacSha256Hex(payload, InpSharedSecret);
   if(StringLen(signature) == 0)
   {
      Print("TradePilotEA: Unable to compute signature.");
      return false;
   }

   string headers =
      "Content-Type: application/json\r\n" +
      "X-Source-Id: " + InpSourceId + "\r\n" +
      "X-Timestamp: " + timestampText + "\r\n" +
      "X-Nonce: " + nonce + "\r\n" +
      "X-Signature: " + signature + "\r\n";

   char data[];
   StringToCharArray(body, data, 0, WHOLE_ARRAY, CP_UTF8);
   if(ArraySize(data) > 0)
   {
      ArrayResize(data, ArraySize(data) - 1);
   }

   char result[];
   string responseHeaders;

   ResetLastError();
   int status = WebRequest("POST", InpConnectorUrl, headers, InpTimeoutMs, data, result, responseHeaders);
   int errorCode = GetLastError();

   if(status >= 200 && status < 300)
   {
      return true;
   }

   if(status == -1)
   {
      Print("TradePilotEA: WebRequest failed. attempt=", attempt, ", error=", errorCode);
      return false;
   }

   string responseBody = CharArrayToString(result, 0, WHOLE_ARRAY, CP_UTF8);
   Print("TradePilotEA: Connector rejected snapshot. attempt=", attempt, ", status=", status, ", body=", responseBody);
   return false;
}

string BuildSnapshotJson()
{
   string json = "{";
   json += "\"sourceId\":\"" + EscapeJson(InpSourceId) + "\",";
   json += "\"timestampUtc\":\"" + ToIsoUtc((datetime)TimeGMT()) + "\",";
   json += "\"account\":" + BuildAccountJson() + ",";
   json += "\"positions\":" + BuildPositionsJson() + ",";
   json += "\"orders\":" + BuildOrdersJson();
   json += "}";
   return json;
}

string BuildAccountJson()
{
   string broker = EscapeJson(AccountInfoString(ACCOUNT_COMPANY));
   string server = EscapeJson(AccountInfoString(ACCOUNT_SERVER));
   long login = AccountInfoInteger(ACCOUNT_LOGIN);
   string currency = EscapeJson(AccountInfoString(ACCOUNT_CURRENCY));

   double balance = AccountInfoDouble(ACCOUNT_BALANCE);
   double equity = AccountInfoDouble(ACCOUNT_EQUITY);
   double margin = AccountInfoDouble(ACCOUNT_MARGIN);
   double freeMargin = AccountInfoDouble(ACCOUNT_MARGIN_FREE);
   double marginLevel = AccountInfoDouble(ACCOUNT_MARGIN_LEVEL);

   string json = "{";
   json += "\"broker\":\"" + broker + "\",";
   json += "\"server\":\"" + server + "\",";
   json += "\"login\":" + LongToString(login) + ",";
   json += "\"currency\":\"" + currency + "\",";
   json += "\"balance\":" + FormatNumber(balance) + ",";
   json += "\"equity\":" + FormatNumber(equity) + ",";
   json += "\"margin\":" + FormatNumber(margin) + ",";
   json += "\"freeMargin\":" + FormatNumber(freeMargin) + ",";
   json += "\"marginLevel\":" + FormatNumber(marginLevel);
   json += "}";
   return json;
}

string BuildPositionsJson()
{
   string json = "[";
   int total = PositionsTotal();
   bool first = true;

   for(int i = 0; i < total; i++)
   {
      ulong ticket = PositionGetTicket(i);
      if(ticket == 0)
      {
         continue;
      }

      string symbol = EscapeJson(PositionGetString(POSITION_SYMBOL));
      long type = PositionGetInteger(POSITION_TYPE);
      string side = type == POSITION_TYPE_SELL ? "sell" : "buy";
      double volume = PositionGetDouble(POSITION_VOLUME);
      double openPrice = PositionGetDouble(POSITION_PRICE_OPEN);
      double stopLoss = PositionGetDouble(POSITION_SL);
      double takeProfit = PositionGetDouble(POSITION_TP);
      double currentPrice = PositionGetDouble(POSITION_PRICE_CURRENT);
      double profit = PositionGetDouble(POSITION_PROFIT);

      if(!first)
      {
         json += ",";
      }

      json += "{";
      json += "\"ticket\":" + ULongToString(ticket) + ",";
      json += "\"symbol\":\"" + symbol + "\",";
      json += "\"side\":\"" + side + "\",";
      json += "\"volume\":" + FormatNumber(volume) + ",";
      json += "\"openPrice\":" + FormatNumber(openPrice) + ",";
      json += "\"stopLoss\":" + FormatNumber(stopLoss) + ",";
      json += "\"takeProfit\":" + FormatNumber(takeProfit) + ",";
      json += "\"currentPrice\":" + FormatNumber(currentPrice) + ",";
      json += "\"profit\":" + FormatNumber(profit);
      json += "}";

      first = false;
   }

   json += "]";
   return json;
}

string BuildOrdersJson()
{
   string json = "[";
   int total = OrdersTotal();
   bool first = true;

   for(int i = 0; i < total; i++)
   {
      ulong ticket = OrderGetTicket(i);
      if(ticket == 0)
      {
         continue;
      }

      string symbol = EscapeJson(OrderGetString(ORDER_SYMBOL));
      string type = EscapeJson(MapOrderType((ENUM_ORDER_TYPE)OrderGetInteger(ORDER_TYPE)));
      double volume = OrderGetDouble(ORDER_VOLUME_CURRENT);
      double price = OrderGetDouble(ORDER_PRICE_OPEN);
      double stopLoss = OrderGetDouble(ORDER_SL);
      double takeProfit = OrderGetDouble(ORDER_TP);
      datetime timeSetup = (datetime)OrderGetInteger(ORDER_TIME_SETUP);

      if(!first)
      {
         json += ",";
      }

      json += "{";
      json += "\"ticket\":" + ULongToString(ticket) + ",";
      json += "\"symbol\":\"" + symbol + "\",";
      json += "\"type\":\"" + type + "\",";
      json += "\"volume\":" + FormatNumber(volume) + ",";
      json += "\"price\":" + FormatNumber(price) + ",";
      json += "\"stopLoss\":" + FormatNumber(stopLoss) + ",";
      json += "\"takeProfit\":" + FormatNumber(takeProfit) + ",";
      json += "\"timeUtc\":\"" + ToIsoUtc(timeSetup) + "\"";
      json += "}";

      first = false;
   }

   json += "]";
   return json;
}

string MapOrderType(const ENUM_ORDER_TYPE orderType)
{
   switch(orderType)
   {
      case ORDER_TYPE_BUY: return "buy";
      case ORDER_TYPE_SELL: return "sell";
      case ORDER_TYPE_BUY_LIMIT: return "buy_limit";
      case ORDER_TYPE_SELL_LIMIT: return "sell_limit";
      case ORDER_TYPE_BUY_STOP: return "buy_stop";
      case ORDER_TYPE_SELL_STOP: return "sell_stop";
      case ORDER_TYPE_BUY_STOP_LIMIT: return "buy_stop_limit";
      case ORDER_TYPE_SELL_STOP_LIMIT: return "sell_stop_limit";
      default: return "unknown";
   }
}

string GenerateNonce()
{
   long timePart = (long)TimeLocal();
   ulong microPart = GetMicrosecondCount();
   int randomPart = MathRand();
   return StringFormat("%I64u-%I64d-%d", microPart, timePart, randomPart);
}

string ComputeHmacSha256Hex(const string payload, const string secret)
{
   uchar payloadBytes[];
   uchar keyBytes[];
   uchar hashBytes[];

   StringToCharArray(payload, payloadBytes, 0, WHOLE_ARRAY, CP_UTF8);
   if(ArraySize(payloadBytes) > 0)
   {
      ArrayResize(payloadBytes, ArraySize(payloadBytes) - 1);
   }

   StringToCharArray(secret, keyBytes, 0, WHOLE_ARRAY, CP_UTF8);
   if(ArraySize(keyBytes) > 0)
   {
      ArrayResize(keyBytes, ArraySize(keyBytes) - 1);
   }

   if(!CryptEncode(CRYPT_HASH_SHA256, payloadBytes, keyBytes, hashBytes))
   {
      Print("TradePilotEA: CryptEncode(CRYPT_HASH_SHA256) failed. error=", GetLastError());
      return "";
   }

   return BytesToHex(hashBytes);
}

string BytesToHex(const uchar &bytes[])
{
   string value = "";
   int count = ArraySize(bytes);
   for(int i = 0; i < count; i++)
   {
      value += StringFormat("%02x", bytes[i]);
   }
   return value;
}

string EscapeJson(const string text)
{
   string escaped = "";
   int len = StringLen(text);

   for(int i = 0; i < len; i++)
   {
      string c = StringSubstr(text, i, 1);

      if(c == "\\")
         escaped += "\\\\";
      else if(c == "\"")
         escaped += "\\\"";
      else if(c == "\b")
         escaped += "\\b";
      else if(c == "\f")
         escaped += "\\f";
      else if(c == "\n")
         escaped += "\\n";
      else if(c == "\r")
         escaped += "\\r";
      else if(c == "\t")
         escaped += "\\t";
      else
         escaped += c;
   }

   return escaped;
}

string ToIsoUtc(datetime value)
{
   string dateTime = TimeToString(value, TIME_DATE | TIME_SECONDS);
   StringReplace(dateTime, ".", "-");
   StringReplace(dateTime, " ", "T");
   return dateTime + "Z";
}

string FormatNumber(const double value)
{
   return DoubleToString(value, 8);
}

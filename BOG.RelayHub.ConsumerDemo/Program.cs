using BOG.RelayHub.Client;
using BOG.RelayHub.Client.Entity;
using BOG.RelayHub.Common.Entity;
using BOG.SwissArmyKnife;
using Figgle;
using System.Net;
using System.Text;

namespace BOG.RelayHub.ConsumerDemo
{
    class Program
    {
        const string ChannelName = "TESTER-A";
        const string DefaultBaseUri = "http://localhost:5050";

        static string OutputFolder = @"C:\Temp\BOG.RelayHub.ConsumerDemo";

        static RelayHubApiConfig _CfgTokenAuthValid = new RelayHubApiConfig
        {
            BaseURI = DefaultBaseUri,
            AdministrativeTokenValue = "RHA-3389A91A-F25C-48EC-A4C9-F3AB05A07065",
            UsageTokenValue = "RHU-4A483566-B82B-43FE-BB5E-7FBEA74802D4",
            ExecutiveTokenValue = "RHE-E9825143-8C1C-4169-9532-8AD4B6AA29E1",
            IgnoreBadSSL = true,
            TimeoutSeconds = 90
        };

        static RelayHubRestCalls _ApiTokenAuthValid;

        static RelayHubApiConfig _CfgTokenAuthFail = new RelayHubApiConfig
        {
            BaseURI = DefaultBaseUri,
            AdministrativeTokenValue = "RHA-Invalid",
            UsageTokenValue = "RHU-Invalid",
            ExecutiveTokenValue = "RHE-Invalid",
            IgnoreBadSSL = true,
            TimeoutSeconds = 90
        };

        static RelayHubRestCalls _ApiTokenAuthFail;

        static Dictionary<int, int> _CarpetBombingTotals = new Dictionary<int, int>();
        static Dictionary<int, int> _CarpetCleanupTotals = new Dictionary<int, int>();

        static int FileIndex = 0;

        static void Main(string[] args)
        {
            Console.WriteLine(FiggleFonts.Roman.Render("BOG.RelayHub.Demo"));
            if (args.Length == 1 && !string.IsNullOrWhiteSpace(args[0]))
            {
                _CfgTokenAuthFail.BaseURI = args[0];
                _CfgTokenAuthValid.BaseURI = args[0];
                Console.WriteLine($"Base Uri (from argument): {args[0]}");
            }
            else
            {
                Console.WriteLine($"Base Uri (default): {DefaultBaseUri}");
            }

            _ApiTokenAuthValid = new RelayHubRestCalls(_CfgTokenAuthValid);
            _ApiTokenAuthFail = new RelayHubRestCalls(_CfgTokenAuthFail);

            if (Directory.Exists(OutputFolder))
            {
                Directory.Delete(OutputFolder, true);
            }
            Directory.CreateDirectory(OutputFolder);
            Console.WriteLine();
            Console.WriteLine($"Output folder: {OutputFolder}");

            var firstPass = true;
            var failed = false;

            while (firstPass)
            {
                firstPass = false;

                Console.Write("CheckHeartbeat: ");
                var result = _ApiTokenAuthValid.CheckHeartbeat();
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    failed = true;
                    continue;
                }

                Console.Write($"Security reset: ");
                result = _ApiTokenAuthValid.ResetSecurityList();
                Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    failed = true;
                    continue;
                }

                Console.Write("ListAllChannels: ");
                result = _ApiTokenAuthValid.ListAllChannels();
                Console.WriteLine(result.HandleAs.ToString());
                CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                switch (result.HandleAs)
                {
                    case RelayHubApiResultHandling.Success:
                    case RelayHubApiResultHandling.ItemDoesNotExist:
                    case RelayHubApiResultHandling.NoSuchChannel:
                        break;
                    default:
                        failed = true;
                        continue;
                }

                Console.Write("DeleteAllChannels: ");
                result = _ApiTokenAuthValid.DeleteAllChannels();
                Console.WriteLine(result.HandleAs.ToString());
                switch (result.HandleAs)
                {
                    case RelayHubApiResultHandling.Success:
                    case RelayHubApiResultHandling.ItemDoesNotExist:
                    case RelayHubApiResultHandling.NoSuchChannel:
                        break;
                    default:
                        CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                        continue;
                }

                Console.WriteLine($"Running Test w/Channel {ChannelName}");
                Console.Write("ChannelExists: ");
                result = _ApiTokenAuthValid.ChannelExists(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                switch (result.HandleAs)
                {
                    case RelayHubApiResultHandling.Success:
                    case RelayHubApiResultHandling.ItemDoesNotExist:
                    case RelayHubApiResultHandling.NoSuchChannel:
                        break;
                    default:
                        CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                        continue;
                }

                if (result.HandleAs == RelayHubApiResultHandling.Success)
                {
                    Console.Write("ChannelRemove: ");
                    result = _ApiTokenAuthValid.ChannelRemove(ChannelName);
                    Console.WriteLine(result.HandleAs.ToString());
                    switch (result.HandleAs)
                    {
                        case RelayHubApiResultHandling.Success:
                        case RelayHubApiResultHandling.NoSuchChannel:
                            break;
                        default:
                            CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                            continue;
                    }
                }

                Console.Write("ChannelCreate: ");
                result = _ApiTokenAuthValid.ChannelCreate(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

#if TRUE

                Console.Write("ReferenceGetKeyList (PRE): ");
                result = _ApiTokenAuthValid.ReferenceGetKeyList(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                {
                    continue;
                }

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("ReferenceWrite (HTML_REF_DATA): ");
                using (var sr = new StreamReader(@".\Large_Test_Data.html"))
                {
                    result = _ApiTokenAuthValid.ReferenceWrite(ChannelName, "HTML_REF_DATA", sr.ReadToEnd());
                }
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("ReferenceWrite (HTML_REF_DATA): ");
                using (var sr = new StreamReader(@".\Large_Test_Data.json"))
                {
                    result = _ApiTokenAuthValid.ReferenceWrite(ChannelName, "HTML_REF_DATA", sr.ReadToEnd());
                }
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("ReferenceWrite (HTML_REF_SMALL): ");
                using (var sr = new StreamReader(@".\Small_Test_Data.html"))
                {
                    result = _ApiTokenAuthValid.ReferenceWrite(ChannelName, "HTML_REF_SMALL", sr.ReadToEnd());
                }
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("ReferenceKeys (GET): ");
                result = _ApiTokenAuthValid.ReferenceGetKeyList(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    continue;
                }

                Console.Write("ReferenceRead (HTML-REF-DATA): ");
                result = _ApiTokenAuthValid.ReferenceRead(ChannelName, "HTML-REF-DATA");
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

                Console.Write("ReferenceRead (HTML_REF_DATA): ");
                result = _ApiTokenAuthValid.ReferenceRead(ChannelName, "HTML_REF_DATA");
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                var isSame = false;
                using (var sr = new StreamReader(@".\Large_Test_Data.json"))
                {
                    isSame = string.Compare(result.Payload, sr.ReadToEnd(), false) == 0;
                }
                if (!isSame)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    Console.WriteLine("Data does not match what was SET in reference");
                    continue;
                }

                Console.Write("ReferenceDelete (HTML-REF-DATA): ");
                result = _ApiTokenAuthValid.ReferenceDelete(ChannelName, "HTML-REF-DATA");
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("Dequeue (Recipient-Bob): ");
                result = _ApiTokenAuthValid.Dequeue(ChannelName, "Recipient-Bob");
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }

                Console.Write("Dequeue (Recipient-Bob): ");
                result = _ApiTokenAuthValid.Dequeue(ChannelName, "Recipient-Bob");
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    break;
                }

                Console.Write("Enqueue (Recipient-Bob): ");
                var cannedPhrases = new string[]
                {
                    "The luck of the old dog",
                    "Brutus tells Ceasar to talk with Shakespeare",
                    "Blind man down"
                };
                var count = 0;
                var maxCount = 500;
                var index = 0;
                string mainPayload;
                using (var sr = new StreamReader(@".\Large_Test_Data.json"))
                {
                    mainPayload = sr.ReadToEnd();
                }

                while (!failed && count++ <= maxCount)
                {
                    Console.Write($"Enqueue (Recipient-Bob) ... {count} of {maxCount}: ");
                    var sb = new StringBuilder();
                    sb.Append(Formatting.RJLZ(count, 5) + ": ");
                    sb.AppendLine(cannedPhrases[index % 3]);
                    sb.Append(mainPayload);
                    result = _ApiTokenAuthValid.Enqueue(ChannelName, "Recipient-Bob", sb.ToString());
                    Console.WriteLine(result.HandleAs.ToString());
                    if (result.HandleAs != RelayHubApiResultHandling.Success)
                    {
                        CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                        failed = (count < maxCount || result.HandleAs != RelayHubApiResultHandling.LimitationError);
                        break;
                    }
                }
                if (failed) continue;

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("Dequeue (Recipient-Bob): ");
                index = 0;
                count = 0;
                while (!failed && count++ < maxCount)
                {
                    Console.Write($"Dequeue (Recipient-Bob) ... {count} of {maxCount}: ");
                    var sb = new StringBuilder();
                    sb.Append(Formatting.RJLZ(count, 5) + ": ");
                    sb.AppendLine(cannedPhrases[index % 3]);
                    sb.Append(mainPayload);

                    result = _ApiTokenAuthValid.Dequeue(ChannelName, "Recipient-Bob");
                    Console.WriteLine(result.HandleAs.ToString());
                    failed = result.HandleAs != RelayHubApiResultHandling.Success;
                    if (failed || string.Compare(result.Payload, sb.ToString(), false) != 0)
                    {
                        CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                        failed = true;
                        break;
                    }
                }
                if (failed) continue;

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.Write("Dequeue (Recipient-Bob, ItemDoesNotExist): ");
                result = _ApiTokenAuthValid.Dequeue(ChannelName, "Recipient-Bob");
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    failed = true;
                    break;
                }
                if (failed) continue;

                CarpetBombing();

#endif

                Console.Write("Statistics for channel: ");
                result = _ApiTokenAuthValid.ChannelStatistics(ChannelName);
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                CreateOutputFile(
                    false,
                    ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(
                        ObjectJsonSerializer<ChannelStatistics>.CreateObjectFormat(result.Payload), true)
                );

                Console.WriteLine("");
                Console.WriteLine("***********************************");
                Console.WriteLine("  Bad Token Denials ....");
                Console.WriteLine("***********************************");
                Console.WriteLine("");

                Console.Write("GetSecurityList (empty): ");
                result = _ApiTokenAuthValid.GetSecurityList();
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                var o = ObjectJsonSerializer<Dictionary<string, Security>>.CreateObjectFormat(result.Payload);
                CreateOutputFile(false, ObjectJsonSerializer<Dictionary<string, Security>>.CreateDocumentFormat(o, true));

                var pass = 0;
                var forceExit = false;
                while (++pass <= 2 && !forceExit)
                {
                    var limit451Count = 0;
                    while (limit451Count < 4 && !forceExit)
                    {
                        limit451Count++;
                        var index1 = 0;
                        var has451Count = 0;
                        DateTime WaitUntil = DateTime.MinValue;
                        while (has451Count < limit451Count && index1 < 30)
                        {
                            index1++;
                            Console.Write($"Invalid token {index1}: ");
                            result = _ApiTokenAuthFail.ChannelStatistics(ChannelName);
                            Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                            if (result.HandleAs != RelayHubApiResultHandling.AuthError)
                            {
                                CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                                forceExit = true;
                                break;
                            }
                            if (result.StatusCode == (int)HttpStatusCode.UnavailableForLegalReasons)
                            {
                                has451Count++;
                                if (has451Count == limit451Count)
                                {
                                    Console.Write($"GetSecurityList: ");
                                    result = _ApiTokenAuthValid.GetSecurityList();
                                    Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                                    if (result.HandleAs != RelayHubApiResultHandling.Success)
                                    {
                                        CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                                        forceExit = true;
                                        continue;
                                    }
                                    var o2 = ObjectJsonSerializer<Dictionary<string, Security>>.CreateObjectFormat(result.Payload);
                                    WaitUntil = o2[o2.Keys.ToList()[0]].DenyAccessAttemptsUntil.AddSeconds(-2);
                                    CreateOutputFile(false, ObjectJsonSerializer<Dictionary<string, Security>>.CreateDocumentFormat(o2, true));
                                }
                            }
                        }
                        if (has451Count == 0 && index1 == 30)
                        {
                            Console.Write("Response Code 451 never received.");
                            forceExit = true;
                            break;
                        }

                        switch (pass)
                        {
                            case 1: // manual reset for security
                                Console.Write($"Security reset: ");
                                result = _ApiTokenAuthValid.ResetSecurityList();
                                Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                                if (result.HandleAs != RelayHubApiResultHandling.Success)
                                {
                                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                                    forceExit = true;
                                    continue;
                                }
                                break;

                            case 2: // timeout expires for wait
                                var iterationsLeft = 10;

                                Console.WriteLine(string.Format("Waiting until: {0:s} ...", WaitUntil));
                                while (DateTime.Now < WaitUntil) Thread.Sleep(500);
                                while (iterationsLeft >= 0)
                                {
                                    Console.Write($"Check if channel exists (access check) {iterationsLeft}: ");
                                    result = _ApiTokenAuthValid.ChannelExists(ChannelName);
                                    Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                                    if (result.HandleAs == RelayHubApiResultHandling.AuthError && result.StatusCode == (int)HttpStatusCode.UnavailableForLegalReasons)
                                    {
                                        Console.Write($"GetSecurityList: ");
                                        result = _ApiTokenAuthValid.GetSecurityList();
                                        Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                                        if (result.HandleAs != RelayHubApiResultHandling.Success)
                                        {
                                            CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                                            forceExit = true;
                                            continue;
                                        }
                                        Thread.Sleep(1000);
                                        iterationsLeft--;
                                    }
                                    else if (result.HandleAs == RelayHubApiResultHandling.Success)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                                        forceExit = true;
                                        break;
                                    }
                                }
                                forceExit = (iterationsLeft == -1);
                                break;
                        }
                        Console.Write($"GetSecurityList: ");
                        result = _ApiTokenAuthValid.GetSecurityList();
                        Console.WriteLine($"{result.HandleAs} ({result.StatusCode}): {result.StatusDetail} ");
                        if (result.HandleAs != RelayHubApiResultHandling.ItemDoesNotExist)
                        {
                            CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                            forceExit = true;
                            continue;
                        }
                        if (result.HandleAs == RelayHubApiResultHandling.Success)
                        {
                            var o1 = ObjectJsonSerializer<Dictionary<string, Security>>.CreateObjectFormat(result.Payload);
                            CreateOutputFile(false, ObjectJsonSerializer<Dictionary<string, Security>>.CreateDocumentFormat(o1, true));
                        }
                    }
                }
                failed |= forceExit;
                if (failed) continue;

                Console.Write("GetSecurityList (one entry): ");
                result = _ApiTokenAuthValid.GetSecurityList();
                Console.WriteLine(result.HandleAs.ToString());
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(false, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                    continue;
                }
                var o4 = ObjectJsonSerializer<Dictionary<string, Security>>.CreateObjectFormat(result.Payload);
                CreateOutputFile(false, ObjectJsonSerializer<Dictionary<string, Security>>.CreateDocumentFormat(o4, true));
            }

            Console.WriteLine();
            Console.Write("Testing ends: ");
            if (failed)
            {
                Console.Write("FAILURE");
            }
            else
            {
                Console.Write("SUCCESSFUL");
            }
            Console.WriteLine(" ... press ENTER to clear");
            Console.ReadLine();
        }

        private static void CarpetBombing()
        {
            var cannedPhrases = new string[]
            {
                    "The luck of the old dog",
                    "Brutus tells Ceasar to talk with Shakespeare",
                    "Blind man down"
            };
            var IndexQueue = new List<int>();

            var maxCount = 500;
            var count = 0;
            string mainPayload;
            var _Lock = new object();
            using (var sr = new StreamReader(@".\Large_Test_Data.json"))
            {
                mainPayload = sr.ReadToEnd();
            }

            for (count = 0; count < 3; count++) _CarpetBombingTotals.Add(count, 0);
            for (count = 0; count < 3; count++) _CarpetCleanupTotals.Add(count, 0);

            Console.WriteLine("");
            Console.WriteLine("***********************************");
            Console.WriteLine("  Bombs away....");
            Console.WriteLine("***********************************");
            Console.WriteLine("");

            var HandleAsCounts = new Dictionary<RelayHubApiResultHandling, int>();
            foreach (var handleAs in Enum.GetValues<RelayHubApiResultHandling>()) HandleAsCounts.Add(handleAs, 0);

            var totalDropped = 0;
            var totalDroppedSuccessful = 0;
            var forLoopResult = Parallel.For(0, maxCount - 1, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (x, state) =>
            {
                var _threadApi = new RelayHubRestCalls(_CfgTokenAuthValid);
                var consOut = new StringBuilder();
                consOut.Append($"Enqueue (Recipient-Bob) ... {x} - {x % 3}: ");
                var sb = new StringBuilder();
                sb.Append(Formatting.RJLZ(x, 5) + ": ");
                sb.AppendLine(cannedPhrases[x % 3]);
                sb.Append(mainPayload);
                var result = _threadApi.Enqueue(ChannelName, "Recipient-Bob", sb.ToString());
                lock (_Lock)
                {
                    totalDropped++;
                }
                consOut.AppendLine($"{result.HandleAs} == {result.StatusCode}");
                if (!HandleAsCounts.Keys.Contains(result.HandleAs)) HandleAsCounts.Add(result.HandleAs, 0);
                HandleAsCounts[result.HandleAs]++;
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    consOut.AppendLine(ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                }
                else
                {
                    totalDroppedSuccessful++;
                }
                lock (_Lock)
                {
                    _CarpetBombingTotals[x % 3]++;
                }
                // lock (lockConsOutStream)
                // {
                Console.Write(consOut.ToString());
                // }
            });

            Console.WriteLine("");
            Console.WriteLine("***********************************");
            Console.WriteLine("  Cleanup....");
            Console.WriteLine("***********************************");
            Console.WriteLine("");

            count = 0;
            var totalPulled = 0;
            var totalPulledSuccessful = 0;
            var failOut = 20;

            while (count <= maxCount && failOut >= 0)
            {
                Console.Write($"Dequeue (Recipient-Bob) ... {count} of {maxCount}: ");
                var result = _ApiTokenAuthValid.Dequeue(ChannelName, "Recipient-Bob");
                Console.Write(result.HandleAs.ToString());
                Console.Write(": ");
                if (result.HandleAs == RelayHubApiResultHandling.ServerError)
                {
                    Console.WriteLine("Wait 2 seconds and retry ...");
                    Thread.Sleep(2000);
                    failOut--;
                    continue;
                }
                if (result.HandleAs == RelayHubApiResultHandling.ItemDoesNotExist) break;
                totalPulled++;
                count++;
                if (result.HandleAs != RelayHubApiResultHandling.Success)
                {
                    CreateOutputFile(true, ObjectJsonSerializer<RelayHubApiResult>.CreateDocumentFormat(result, true));
                }
                else
                {
                    var firstLine = result.Payload.Split(new string[] { "\r\n" }, StringSplitOptions.None)[0];
                    var index = firstLine.IndexOf(":");
                    var intPart = string.Empty;
                    int ID = 0;
                    int modulus = 0;

                    Console.WriteLine($"First Line: {firstLine}");
                    var noGood = (index < 0);
                    if (noGood)
                    {
                        Console.WriteLine($"No colon found: \"{firstLine}\"");
                    }
                    else
                    {
                        intPart = firstLine.Substring(0, index);
                        noGood = !int.TryParse(intPart, out ID);
                        if (noGood)
                        {
                            Console.WriteLine($"No ID before colon: \"{firstLine}\"");
                        }
                        else
                        {
                            noGood = !(ID >= 0 && ID <= maxCount);
                        }
                        if (noGood)
                        {
                            Console.WriteLine($"ID {ID} must be 0 <= x <= {maxCount}: \"{firstLine}\"");
                        }
                        else
                        {
                            modulus = ID % 3;
                            noGood = firstLine.IndexOf(cannedPhrases[modulus], StringComparison.Ordinal) != (index + 2);
                            if (noGood)
                            {
                                Console.WriteLine($"Expected text not found: {cannedPhrases[modulus]}: \"{firstLine}\"");
                            }
                            else
                            {
                                Console.WriteLine($"PASSED");
                                totalPulledSuccessful++;
                                _CarpetCleanupTotals[modulus]++;
                            }
                        }
                    }
                    if (!noGood)
                    {
                        if (IndexQueue.Contains(ID)) IndexQueue.Remove(ID);
                    }
                }
            }

            Console.WriteLine("");
            Console.WriteLine($"totalDropped: ............... {totalDropped}");
            Console.WriteLine($"totalDroppedSuccessful: ..... {totalDroppedSuccessful}");
            for (count = 0; count < 3; count++) Console.WriteLine($"      {count}: {_CarpetBombingTotals[count]}");

            Console.WriteLine($"RelayHubApiResultHandling: .. {HandleAsCounts.Keys.Count()}");
            foreach (var handleAsName in Enum.GetNames<RelayHubApiResultHandling>())
            {
                var handleAs = Enum.Parse<RelayHubApiResultHandling>(handleAsName);
                Console.WriteLine(string.Format("      {0}: {1}", handleAs, HandleAsCounts[handleAs]));
            }
            Console.WriteLine("");
            Console.WriteLine($"totalPulled: ............ {totalPulled}");
            Console.WriteLine($"totalPulledSuccessful: .. {totalPulledSuccessful}");
            for (count = 0; count < 3; count++) Console.WriteLine($"      {count}: {_CarpetCleanupTotals[count]}");

            Console.WriteLine("");
            Console.WriteLine("***********************************");
            Console.WriteLine("  Done....");
            Console.WriteLine("***********************************");
            Console.WriteLine("");
        }

        static void CreateOutputFile(bool isError, string content)
        {
            using (var sw = new StreamWriter(Path.Combine(OutputFolder, string.Format("{0}-{1}.json", FileIndex.ToString().PadLeft(5, '0'), isError ? "E" : "x"))))
            {
                sw.WriteLine(content);
            }
            Console.WriteLine(content);
            FileIndex++;
        }
    }
}
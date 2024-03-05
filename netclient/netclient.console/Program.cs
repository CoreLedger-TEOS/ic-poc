using System.Formats.Cbor;
using System.Text.Json;
using System.Text.Json.Serialization;
using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Agents.Http;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Models;
using netclient.console;
using netclient.console.Model;

var options = new UploadOptions { };
IIdentity identity = null;
if (options.IdentityPEMFilePath != null)
{
	identity = IdentityUtil.FromPemFile(options.IdentityPEMFilePath, /*options.IdentityPassword*/null);
}
Uri httpBoundryNodeUrl = new(options.Url);
var agent = new HttpAgent(identity, httpBoundryNodeUrl);
Principal canisterId = Principal.FromText(options.CanisterId);

var theSampleClient = new TheSampleClient(agent, identity, canisterId);
await theSampleClient.EmulateTxServerBehavior();

class UploadOptions
{
	public string Url { get; set; } = "http://127.0.0.1:4943";
	//public string Url { get; set; } = "https://ic0.app";

	public string CanisterId { get; set; } = "bkyz2-fmaaa-aaaaa-qaaaq-cai";
	//public string CanisterId { get; set; } = "wlexz-2aaaa-aaaab-qadeq-cai";

	public string FilePath { get; set; }

	public string Key { get; set; }

	public string ContentType { get; set; }

	public string Encoding { get; set; }

	public string IdentityPEMFilePath { get; set; } = "./pk.pem";

	public string IdentityPassword { get; set; } = "123";
};

class TheSampleClient
{
	private readonly HttpAgent _agent;
	private readonly IIdentity _identity;
	private readonly Principal _canisterId;

	public TheSampleClient(HttpAgent agent, IIdentity identity, Principal canisterId)
	{
		_agent = agent;
		_identity = identity;
		_canisterId = canisterId;
	}

	public async Task EmulateTxServerBehavior()
	{
		string assetId = "F94E2AD9DD5CBBC041430001";

		// 1. create tx
		Console.WriteLine("> create tx...");
		var tx = CreateTx(assetId);

		// 2. sign tx
		Console.WriteLine("> sign tx...");
		var signedTx = SignTx(tx);

		// 3. submit signed tx
		Console.WriteLine("> submit signed tx...");
		var requestId = await SubmitSignedTxAsync(signedTx);

		// 4. check status
		Console.WriteLine("> check status...");
		await WaitForCompletionAsync(requestId);

		// 5. get asset
		Console.WriteLine("> get asset...");
		Asset asset = await GetAssetAsync(assetId);

		var jsonOpt = new JsonSerializerOptions { WriteIndented = true };
		jsonOpt.Converters.Add(new PrincipalJsonConverter());
		Console.WriteLine(JsonSerializer.Serialize(asset, jsonOpt));

		Console.WriteLine("Fertig!");
		Console.ReadLine();
	}

	async Task<Asset> GetAssetAsync(string assetId)
	{
		CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(assetId, default));
		QueryResponse response = await _agent.QueryAsync(_canisterId, "getAsset", arg);
		CandidArg reply = response.ThrowOrGetReply();

		return reply.ToObjects<Asset>(default);
	}

	async Task WaitForCompletionAsync(RequestId requestId)
	{
		while (true)
		{
			var reqState = await _agent.GetRequestStatusAsync(_canisterId, requestId);

			Console.WriteLine(reqState?.Type.ToString() ?? "null");

			if (reqState?.Type == RequestStatus.StatusType.Replied)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500));
		}
	}

	Dictionary<string, IHashable> CreateTx(string assetId)
	{
		string method = "createAsset";

		CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(assetId, default));

		var myPrincipal = _identity.GetPublicKey().ToPrincipal();

		var req = BuildRequest(myPrincipal, ICTimestamp.Future(TimeSpan.FromSeconds(10)), method, arg);

		var result = req.BuildHashableItem();
		return result;
	}

	SignedContent SignTx(Dictionary<string, IHashable> content)
	{
		return _identity.SignContent(content);
	}

	// from EdjCase.ICP.Agent
	async Task<RequestId> SubmitSignedTxAsync(SignedContent tx)
	{
		byte[] cborBody = SerializeSignedContent(tx);

		var url = $"/api/v2/canister/{_canisterId.ToText()}/call";

		HttpResponse httpResponse = await _agent.HttpClient.PostAsync(url, cborBody, CancellationToken.None);
		var sha256 = SHA256HashFunction.Create();
		RequestId requestId = RequestId.FromObject(tx.Content, sha256);

		await httpResponse.ThrowIfErrorAsync();
		if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
		{
			// If returns with a body, then an error happened https://forum.dfinity.org/t/breaking-changes-to-the-replica-api-agent-developers-take-note/19651

			byte[] cborBytes = await httpResponse.GetContentAsync();
			var reader = new CborReader(cborBytes);
			CallRejectedResponse response;
			try
			{
				response = CallRejectedResponse.FromCbor(reader);
			}
			catch (Exception ex)
			{
				string message = "Unable to parse call rejected cbor response.\n" +
					"Response bytes: " + ByteUtil.ToHexString(cborBytes);
				throw new Exception(message, ex);
			}
			throw new CallRejectedException(response.Code, response.Message, response.ErrorCode);
		}
		return requestId;
	}

	// from EdjCase.ICP.Agent
	byte[] SerializeSignedContent(SignedContent signedContent)
	{
		var writer = new CborWriter();
		writer.WriteTag(CborTag.SelfDescribeCbor);
		signedContent.WriteCbor(writer);
		return writer.Encode();
	}

	CallRequest BuildRequest(Principal sender, ICTimestamp now, string method, CandidArg arg)
	{
		return new CallRequest(_canisterId, method, arg, sender, now);
	}

	class PrincipalJsonConverter : JsonConverter<Principal>
	{
		public override Principal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			try
			{
				var str = reader.GetString();
				return Principal.FromText(str);
			}
			catch (Exception exception)
			{
				throw new JsonException($"Unable to parse value into Principal type", exception) { };
			}
		}

		public override void Write(Utf8JsonWriter writer, Principal value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}

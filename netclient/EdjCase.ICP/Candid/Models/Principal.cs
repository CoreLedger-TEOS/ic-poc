using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Linq;
using System.Text;

namespace EdjCase.ICP.Candid.Models
{
	/// <summary>
	/// The specific type of principal that is encoded
	/// </summary>
	public enum PrincipalType
	{
		/// <summary>
		/// These are always generated by the IC and have no structure of interest outside of it.
		/// Typically end with 0x01
		/// </summary>
		Opaque,
		/// <summary>
		/// Used if the key is directly used and not delegated/derived.
		/// These have the form `H(public_key) · 0x02` (29 bytes)
		/// </summary>
		SelfAuthenticating,
		/// <summary>
		/// These ids are treated specially when an id needs to be registered. In such a request, whoever requests an id
		/// can provide a derivation_nonce. By hashing that together with the principal of the caller, every principal
		/// has a space of ids that only they can register ids from.
		/// These have the form `H(|registering_principal| · registering_principal · derivation_nonce) · 0x03` (29 bytes)
		/// </summary>
		Derived,
		/// <summary>
		/// Used when there is no authentication/signature
		/// This has the form `0x04`
		/// </summary>
		Anonymous,
		/// <summary>
		/// These have the form of `blob · 0x7f` (29 bytes) where the blob length is between 0 and 28 bytes
		/// </summary>
		Reserved
	}

	/// <summary>
	/// A model representing a principal byte value with helper functions
	/// </summary>
	public class Principal : IHashable, IEquatable<Principal>
	{
		private const byte anonymousSuffix = 4;
		private const byte selfAuthenticatingSuffix = 2;
		/// Byte form of prefix "\x0Aaccount-id"
		private static readonly byte[] accountIdPrefix = { 0x0A, 0x61, 0x63, 0x63, 0x6F, 0x75, 0x6E, 0x74, 0x2D, 0x69, 0x64 };


		/// <summary>
		/// The kind of the principal
		/// </summary>
		public PrincipalType Type { get; }

		/// <summary>
		/// The raw value of the principal
		/// </summary>
		public byte[] Raw { get; }

		private Principal(PrincipalType type, byte[] raw)
		{
			this.Type = type;
			this.Raw = raw;
		}

		/// <summary>
		/// Converts the principal into its text format, such as "rrkah-fqaaa-aaaaa-aaaaq-cai"
		/// </summary>
		/// <returns>A text version of the principal</returns>
		public string ToText()
		{
			// Add checksum to beginning of byte array
			byte[] checksum = CRC32.ComputeHash(this.Raw);
			byte[] bytesWithChecksum = checksum
				.Concat(this.Raw)
				.ToArray();
			return Base32EncodingUtil.FromBytes(bytesWithChecksum, groupedWithChecksum: true);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return this.ToText();
		}

		/// <summary>
		/// Converts the raw principal value into a hex string in all caps with no delimiters
		/// </summary>
		/// <returns>Hex value as a string</returns>
		public string ToHex()
		{
			return ByteUtil.ToHexString(this.Raw);
		}

		/// <summary>
		/// Generates an account identifier from a sub-account byte array.
		/// </summary>
		/// <remarks>
		/// This method constructs a ledger account identifier by concatenating a fixed prefix, the principal's raw byte array,
		/// and a sub-account byte array. It computes a SHA-224 hash on this concatenated byte array, then calculates a CRC-32
		/// checksum of the hash. The resulting account identifier is a concatenation of the CRC-32 checksum and the SHA-224 hash.
		///
		/// The method expects a sub-account byte array of exactly 32 bytes in length. If the provided array does not meet this
		/// requirement, an <see cref="ArgumentException"/> is thrown.
		/// 
		/// The account identifier format follows the specification:
		/// account_identifier(principal, subaccount_identifier) = CRC32(h) || h
		/// where h = sha224("\x0Aaccount-id" || principal || subaccount_identifier).
		/// </remarks>
		/// <param name="subAccount">Optional. The sub-account byte array, expected to be 32 bytes in length.If not specified, will not use a subaccount</param>
		/// <returns>A byte array representing the account identifier.</returns>
		/// <exception cref="ArgumentException">Thrown when the sub-account byte array is not 32 bytes in length.</exception>

		public byte[] ToLedgerAccount(byte[]? subAccount)
		{
			if (subAccount == null)
			{
				// Empty byte array of 32 bytes for no subaccount
				subAccount = new byte[32]; 
			}
			// Ensure the subAccount is of expected length (32 bytes)
			if (subAccount.Length != 32)
			{
				throw new ArgumentException("SubAccount must be 32 bytes in length.");
			}

			// Combine the principal's raw byte array with the subAccount byte array
			byte[] data = accountIdPrefix // "\x0Aaccount-id"
				.Concat(this.Raw)
				.Concat(subAccount).ToArray();

			// Compute the SHA224 hash
			SHA224 sha224 = new ();
			byte[] hash = sha224.GenerateDigest(data);

			// Compute the CRC32 checksum
			byte[] checksum = CRC32.ComputeHash(hash);

			// Combine checksum and hash
			byte[] bytesWithChecksum = checksum.Concat(hash).ToArray();

			// Convert to hex string and return
			return bytesWithChecksum;
		}

		/// <summary>
		/// Helper method to create the principal for the Internet Computer management cansiter "aaaaa-aa"
		/// </summary>
		/// <returns>Principal for the management cansiter</returns>
		public static Principal ManagementCanisterId()
		{
			return new Principal(PrincipalType.Opaque, new byte[0]);
		}

		/// <summary>
		/// Converts raw principal bytes to a principal
		/// </summary>
		/// <param name="raw">Byte array of a principal value</param>
		/// <returns>Principal from the bytes</returns>
		public static Principal FromBytes(byte[] raw)
		{
			PrincipalType type = raw.LastOrDefault() switch
			{
				0x02 => PrincipalType.SelfAuthenticating,
				0x03 => PrincipalType.Derived,
				0x04 => PrincipalType.Anonymous,
				0x7f => PrincipalType.Reserved,
				_ => PrincipalType.Opaque
			};

			return new Principal(type, raw);
		}

		/// <summary>
		/// Creates a principal from a non delimited hex string value
		/// </summary>
		/// <param name="hex">A string form of a hex value with no delimiters</param>
		/// <returns>Principal from the hex value</returns>
		public static Principal FromHex(string hex)
		{
			byte[] bytes = ByteUtil.FromHexString(hex);
			return FromBytes(bytes);
		}

		/// <summary>
		/// Creates an anonymous principal
		/// </summary>
		/// <returns>Anonymous principal</returns>
		public static Principal Anonymous()
		{
			return new Principal(PrincipalType.Anonymous, new byte[] { anonymousSuffix });
		}

		/// <summary>
		/// Creates a self authenticating principal with the specified public key
		/// </summary>
		/// <param name="derEncodedPublicKey">DER encoded public key</param>
		/// <returns>Principal from the public key</returns>
		public static Principal SelfAuthenticating(byte[] derEncodedPublicKey)
		{
			byte[] digest = new SHA224().GenerateDigest(derEncodedPublicKey);

			// bytes = digest + selfAuthenticatingSuffix
			byte[] bytes = new byte[digest.Length + 1];
			digest.CopyTo(bytes.AsSpan());
			bytes[bytes.Length - 1] = selfAuthenticatingSuffix;
			return new Principal(PrincipalType.SelfAuthenticating, bytes);
		}

		/// <summary>
		/// Converts a text representation of a principal to a principal
		/// </summary>
		/// <param name="text">The text value of the principal</param>
		/// <returns>Principal based on the text</returns>
		public static Principal FromText(string text)
		{
			string canisterIdNoDash = text
				.ToLower()
				.Replace("-", "");

			byte[] bytes = Base32EncodingUtil.ToBytes(canisterIdNoDash);

			// Remove first 4 bytes which is the checksum
			bytes = bytes
				.AsSpan()
				.Slice(4)
				.ToArray();

			var principal = FromBytes(bytes);
			string parsedText = principal.ToText();
			if (parsedText != text)
			{
				throw new Exception($"Principal '{parsedText}' does not have a valid checksum.");
			}

			return principal;
		}

		/// <inheritdoc />
		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			return hashFunction.ComputeHash(this.Raw);
		}

		/// <inheritdoc />
		public bool Equals(Principal? other)
		{
			if (other == null)
			{
				return false;
			}
			return this.Raw.SequenceEqual(other.Raw);
		}

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			return this.Equals(obj as Principal);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(this.Raw);
		}

		/// <inheritdoc />
		public static bool operator ==(Principal? v1, Principal? v2)
		{
			if (ReferenceEquals(v1, null))
			{
				return ReferenceEquals(v2, null);
			}
			return v1.Equals(v2);
		}

		/// <inheritdoc />
		public static bool operator !=(Principal? v1, Principal? v2)
		{
			if (ReferenceEquals(v1, null))
			{
				return ReferenceEquals(v2, null);
			}
			return !v1.Equals(v2);
		}
	}
}
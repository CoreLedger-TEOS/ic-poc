using System;
using EdjCase.ICP.Candid.Mapping;
using EdjCase.ICP.Candid.Models;

namespace netclient.console.Model
{
	internal class Asset
	{
		[CandidName("issuer")]
		public Principal Issuer { get; set; }

		[CandidName("amendment_count")]
		public uint AmendmentCount { get; set; }

		[CandidName("created_on")]
		public ulong CreatedOn { get; set; }
	}
}

/*
	"issuer": principal;
	"amendment_count": nat32;
	"created_on": nat64;
*/

use candid::{CandidType, Decode, Deserialize, Encode, Nat};
use ic_principal::Principal;
use ic_stable_structures::storable::Bound;
use ic_stable_structures::Storable;
use num_bigint::BigUint;
use std::borrow::Cow;

// qwe validations

#[derive(Ord, PartialOrd, PartialEq, Eq, Clone, CandidType, Deserialize)]
pub struct AccountId {
    pub value: Nat,
}

impl AccountId {
    pub fn new(principal: &Principal, unique_asset_id: &UniqueAssetId) -> AccountId {
        let p_bytes = principal.as_slice();

        let u_bytes = unique_asset_id.value.0.to_bytes_be();
        let u_slice = u_bytes.as_slice();

        let a_bytes = [p_bytes, u_slice].concat();
        let v = Nat::from(BigUint::from_bytes_be(&a_bytes));

        AccountId { value: v }
    }

    pub fn split(&self) -> (Principal, UniqueAssetId) {
        let unique_asset_id_mask_hex =
            "0000000000000000000000000000000000000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF";
        let unique_asset_id_mask_big_uint = big_uint_parse_hex(unique_asset_id_mask_hex);
        let principal_shift = 12 * 8;

        let principal_big_uint: BigUint = &self.value.0 >> principal_shift;
        let bytes = principal_big_uint.to_bytes_be();
        let slice = bytes.as_slice();
        let principal = Principal::from_slice(slice);

        let unique_asset_id = UniqueAssetId::new(&(&self.value.0 & unique_asset_id_mask_big_uint));

        (principal, unique_asset_id)
    }
}

impl Storable for AccountId {
    fn to_bytes(&self) -> Cow<[u8]> {
        Cow::Owned(Encode!(self).unwrap())
    }

    fn from_bytes(bytes: Cow<[u8]>) -> Self {
        Decode!(bytes.as_ref(), Self).unwrap()
    }

    const BOUND: Bound = Bound::Bounded {
        max_size: 62,
        is_fixed_size: true,
    };
}

#[derive(Ord, PartialOrd, PartialEq, Eq, Clone, CandidType, Deserialize)]
pub struct UniqueAssetId {
    pub value: Nat,
}

impl UniqueAssetId {
    pub fn new(value: &BigUint) -> UniqueAssetId {
        let v = Nat::from(value.clone());
        UniqueAssetId { value: v }
    }

    pub fn parse(hex: &str) -> UniqueAssetId {
        let big_uint = big_uint_parse_hex(hex);
        UniqueAssetId { value: Nat::from(big_uint) }
    }

    pub fn to_string(&self) -> String {
        format!("{}", self.value)
    }
}

impl Storable for UniqueAssetId {
    fn to_bytes(&self) -> Cow<[u8]> {
        Cow::Owned(Encode!(self).unwrap())
    }

    fn from_bytes(bytes: Cow<[u8]>) -> Self {
        Decode!(bytes.as_ref(), Self).unwrap()
    }

    const BOUND: Bound = Bound::Bounded {
        max_size: 29,
        is_fixed_size: true,
    };
}

fn big_uint_parse_hex(hex: &str) -> BigUint {
    let bytes = hex::decode(hex).unwrap();
    let result = BigUint::from_bytes_be(&bytes);

    result
}

#[derive(Clone, Debug, CandidType, Deserialize)]
pub struct Asset {
    pub created_on: u64,
    pub issuer: Principal,
    pub amendment_count: u32,
}

impl Storable for Asset {
    fn to_bytes(&self) -> std::borrow::Cow<[u8]> {
        Cow::Owned(Encode!(self).unwrap())
    }

    fn from_bytes(bytes: std::borrow::Cow<[u8]>) -> Self {
        Decode!(bytes.as_ref(), Self).unwrap()
    }

    const BOUND: Bound = Bound::Bounded {
        max_size: 70,
        is_fixed_size: true,
    };
}

#[derive(Clone, Debug, CandidType, Deserialize)]
pub struct AssetUpdate {
    pub asset_id: Nat,
    pub event_id: u16,
}

impl Storable for AssetUpdate {
    fn to_bytes(&self) -> Cow<[u8]> {
        Cow::Owned(Encode!(self).unwrap())
    }

    fn from_bytes(bytes: Cow<[u8]>) -> Self {
        Decode!(bytes.as_ref(), Self).unwrap()
    }

    const BOUND: Bound = Bound::Bounded {
        max_size: 70,
        is_fixed_size: true,
    };
}

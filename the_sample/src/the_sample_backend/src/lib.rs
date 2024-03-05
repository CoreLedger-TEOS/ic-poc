mod types;

use crate::types::{AccountId, Asset, AssetUpdate, UniqueAssetId};
use candid::{Nat, Principal};
use ic_cdk::{query, update};
use ic_stable_structures::memory_manager::{MemoryId, MemoryManager, VirtualMemory};
use ic_stable_structures::{DefaultMemoryImpl, StableBTreeMap, StableLog};
use std::cell::RefCell;

type Memory = VirtualMemory<DefaultMemoryImpl>;

const ASSETS_MEMORY_ID: MemoryId = MemoryId::new(0);
const ASSET_UPDATE_LOG_IX_MEMORY_ID: MemoryId = MemoryId::new(1);
const ASSET_UPDATE_LOG_DATA_MEMORY_ID: MemoryId = MemoryId::new(2);
const TOTAL_UNITS_MEMORY_ID: MemoryId = MemoryId::new(4);
const ACCOUNTS_MEMORY_ID: MemoryId = MemoryId::new(5);

thread_local! {

    static MEMORY_MANAGER: RefCell<MemoryManager<DefaultMemoryImpl>> =
        RefCell::new(MemoryManager::init(DefaultMemoryImpl::default()));

    static ASSETS: RefCell<StableBTreeMap<UniqueAssetId, Asset, Memory>> =
        MEMORY_MANAGER.with(|mm| {
            RefCell::new(StableBTreeMap::init(
                mm.borrow().get(ASSETS_MEMORY_ID)
            ))
        });

    static ASSET_UPDATE_LOG: RefCell<StableLog<AssetUpdate, Memory, Memory>> =
        MEMORY_MANAGER.with(|mm| {
            RefCell::new(StableLog::init(
                mm.borrow().get(ASSET_UPDATE_LOG_IX_MEMORY_ID),
                mm.borrow().get(ASSET_UPDATE_LOG_DATA_MEMORY_ID),
            ).expect("failed to initialize the event log"))
        });

    static ASSETS_UNITS: RefCell<StableBTreeMap<UniqueAssetId, u128, Memory>> =
        MEMORY_MANAGER.with(|mm| {
            RefCell::new(StableBTreeMap::init(
                mm.borrow().get(TOTAL_UNITS_MEMORY_ID)
            ))
        });

    static ACCOUNTS: RefCell<StableBTreeMap<AccountId, u128, Memory>> =
        MEMORY_MANAGER.with(|mm| {
            RefCell::new(StableBTreeMap::init(
                mm.borrow().get(ACCOUNTS_MEMORY_ID)
            ))
        });
}

#[update(name = "createAsset")]
fn create_asset(unique_asset_id_hex: String) {
    let principal_id = ic_cdk::api::caller();
    let now = ic_cdk::api::time();
    let unique_asset_id = UniqueAssetId::parse(&unique_asset_id_hex);

    let asset = Asset {
        issuer: principal_id,
        amendment_count: 0,
        created_on: now,
    };

    ASSETS.with(|asset_store| asset_store.borrow_mut().insert(unique_asset_id.clone(), asset));
    emit_asset_update(&unique_asset_id.value, 1);
}

#[query(name = "getAsset")]
fn get_asset(unique_asset_id_hex: String) -> Asset {
    let unique_asset_id = UniqueAssetId::parse(&unique_asset_id_hex);
    ASSETS.with(|asset_store| asset_store.borrow().get(&unique_asset_id).unwrap())
}

#[query(name = "getEvent")]
fn get_event(i: u64) -> AssetUpdate {
    ASSET_UPDATE_LOG.with(|event_store| event_store.borrow().get(i).unwrap())
}

#[query(name = "getEventCount")]
fn get_event_count() -> u64 {
    let len = ASSET_UPDATE_LOG.with(|event_store| event_store.borrow().len());
    len
}

#[update(name = "createTokens")]
fn create_tokens(unique_asset_id_hex: String, amount: u128) {
    let unique_asset_id = UniqueAssetId::parse(&unique_asset_id_hex);
    ASSETS_UNITS.with(|store| store.borrow_mut().insert(unique_asset_id.clone(), amount));

    let principal_id = ic_cdk::api::caller();

    let account_id = AccountId::new(&principal_id, &unique_asset_id);

    ACCOUNTS.with(|store| {
        store.borrow_mut().insert(account_id, amount);
    })
}

#[query(name = "totalTokens")]
fn total_tokens(unique_asset_id_hex: String) -> u128 {
    let unique_asset_id = UniqueAssetId::parse(&unique_asset_id_hex);
    match ASSETS_UNITS.with(|store| store.borrow().get(&unique_asset_id)) {
        Some(bal) => bal,
        None => 0,
    }
}

#[query(name = "account")]
fn account_tokens(principal: Principal, unique_asset_id_hex: String) -> u128 {
    let unique_asset_id = UniqueAssetId::parse(&unique_asset_id_hex);
    let account_id = AccountId::new(&principal, &unique_asset_id);

    match ACCOUNTS.with(|store| store.borrow().get(&account_id)) {
        Some(bal) => bal,
        None => 0,
    }
}

#[update(name = "transfer")]
fn transfer_tokens(from: Principal, to: Principal, unique_asset_id_hex: String, amount: u128) {
    let unique_asset_id = UniqueAssetId::parse(&unique_asset_id_hex);
    let acc_from = AccountId::new(&from, &unique_asset_id);
    let acc_to = AccountId::new(&to, &unique_asset_id);

    let from_old_balance = ACCOUNTS.with(|store| store.borrow().get(&acc_from).unwrap());
    let from_new_balance = from_old_balance - amount;

    let to_old_balance_option = ACCOUNTS.with(|store| store.borrow().get(&acc_to));
    let to_old_balance = match to_old_balance_option {
        Some(x) => x,
        None => 0,
    };

    let to_new_balance = to_old_balance + amount;

    ACCOUNTS.with(|store| store.borrow_mut().insert(acc_from, from_new_balance));
    ACCOUNTS.with(|store| store.borrow_mut().insert(acc_to, to_new_balance));
}

#[query(name = "hello")]
fn hello_world() -> String {
    let principal_id = ic_cdk::api::caller();

    let unique_asset_id_hex = "F94E2AD9DD5CBBC041430001";
    let unique_asset_id = UniqueAssetId::parse(unique_asset_id_hex);

    let account_id = AccountId::new(&principal_id, &unique_asset_id);
    let parts = account_id.split();

    format!("Principal: {} {}", parts.0, parts.1.to_string())
}

fn emit_asset_update(unique_asset_id: &Nat, event_id: u16) {
    let event = AssetUpdate {
        asset_id: unique_asset_id.clone(),
        event_id: event_id,
    };

    ASSET_UPDATE_LOG.with(|event_store| {
        let _ = event_store.borrow_mut().append(&event);
    })
}

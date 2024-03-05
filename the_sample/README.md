```bash
# Starts the replica, running in the background
dfx start --background

# Deploys your canisters to the replica and generates your candid interface
dfx deploy
```

The commands, that may be used to interact with the sample container from the command line:

```bash

# create an asset ('update' call)
dfx canister call the_sample_backend createAsset '("F94E2AD9DD5CBBC041430004")'

# get an asset ('query' call)
dfx canister call the_sample_backend getAsset '("F94E2AD9DD5CBBC041430004")'

# get total number of 'events'
dfx canister call the_sample_backend getEventCount

# get 'event' data
dfx canister call the_sample_backend getEvent 0

# create asset tokens ('update' call)
dfx canister call the_sample_backend createTokens '("F94E2AD9DD5CBBC041430004", 1000)'

# get total asset tokens ('update' call)
dfx canister call the_sample_backend totalTokens '("F94E2AD9DD5CBBC041430004")'

# get current principal ID
dfx identity get-principal

# get account balance (use principal id from the previous command output)
dfx canister call the_sample_backend account '(principal "r63pu-qjqo3-yvyrr-xhymp-63lj4-oycfx-7r4yr-mspkh-guhdl-vrjkr-cae", "F94E2AD9DD5CBBC041430004")'

# transfer asset tokens to another principal
dfx canister call the_sample_backend transfer '(principal "r63pu-qjqo3-yvyrr-xhymp-63lj4-oycfx-7r4yr-mspkh-guhdl-vrjkr-cae", principal "aegzp-gtkmr-jvvmz-etkwz-pipqe-q4wui-vmv4a-glxhw-ouk4j-ru2p4-7ae", "F94E2AD9DD5CBBC041430004", 50)'

# check that original account decreased
dfx canister call the_sample_backend account '(principal "r63pu-qjqo3-yvyrr-xhymp-63lj4-oycfx-7r4yr-mspkh-guhdl-vrjkr-cae", "F94E2AD9DD5CBBC041430004")'

# check that another account increased
dfx canister call the_sample_backend account '(principal "aegzp-gtkmr-jvvmz-etkwz-pipqe-q4wui-vmv4a-glxhw-ouk4j-ru2p4-7ae", "F94E2AD9DD5CBBC041430004")'
```


To deploy and interact with a sandbox-continer on IC-mainnet, use:

- `dfx deploy --playground` to deploy the container
- add `--network ic` parameter to the commands for interacting with container.
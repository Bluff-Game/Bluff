The release workflow uses `release.py` to validate semantic versions and package the
complete Windows player directory into one ZIP with a manifest and SHA-256 checksum.

`online.py --smoke` validates the committed public Cloud project ID and exercises
real anonymous Authentication and Relay allocation/join APIs. It does not test
Netcode gameplay traffic. Credentials stay in memory, and disposable test players
are deleted at the end. Run regression checks with:

```sh
python3 -m unittest discover -s .github/scripts -p 'test_*.py'
```

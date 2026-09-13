# Test Architecture

Test pyramid: Domain/value objects → unit tests. Application logic → focused unit/contract tests through fake/test implementations of ports. Infrastructure adapters → integration/fixture-based tests. OpenAI gateway vertical paths → small number of end-to-end tests. Not almost exclusively mocks; not almost exclusively expensive E2E.

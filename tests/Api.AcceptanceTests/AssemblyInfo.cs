using Api.AcceptanceTests.Infrastructure;

// One throwaway Postgres database (or schema) per test run, shared by every test class.
[assembly: AssemblyFixture(typeof(PostgresFixture))]

# Investec Bank GraphQL

A GraphQL API interface for Investec's [Open Banking REST API](https://developer.investec.com/programmable-banking/#open-api).

Requirements: [.NET](https://dotnet.microsoft.com/download/dotnet) (5.0 or higher).

To run the server locally (you'll need a client id and secret from Investec's Developer Portal):
```sh
export INVESTEC_CLIENT_ID=<YOUR CLIENT ID HERE>
export INVESTEC_CLIENT_SECRET=<YOUR CLIENT SECRET HERE>

dotnet restore
dotnet run
```

Open [http://localhost:5000](http://localhost:5000) in your browser 

Here is a query you can run:
```graphql
{
  mainAccount {
    number
    productName
    referenceName

    balances {
      current
      available
    }

    transactions(last: 5) {
      transactionDate
      amount
      description
    }
  }
}
```

If you want to play with the code, start it in watch mode, make some changes, and the server will restart
```sh
dotnet watch run
```

# Acknowledgements

- [Investec Open Banking CLI](https://github.com/adrianhopebailie/investec) really helped with capturing the mock JSON responses, and in general with a working example of how to use the Investec Open Banking REST API.

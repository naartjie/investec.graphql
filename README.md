# Investec Bank GraphQL

GraphQL API for [Investec Open Banking](https://developer.investec.com/programmable-banking/#open-api)

Requirements: [.NET Core](https://dotnet.microsoft.com/download)

Run the server locally
```sh
export INVESTEC_CLIENT_ID=blahblahblah
export INVESTEC_CLIENT_SECRET=foofoofar

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
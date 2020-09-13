module BankAPI.Schema

open FSharp.Data.GraphQL
open FSharp.Data.GraphQL.Types
open Giraffe

let AccountBalanceType = Define.Object<Investec.BalancesResponse.Data>( name = "Balance", fields = [
    Define.Field("current", Float, resolve = fun _ctx bal -> float bal.CurrentBalance )
    Define.Field("available", Float, resolve = fun _ctx bal -> float bal.AvailableBalance )
])

let TransactionsType = Define.Object<Investec.TransactionsResponse.Transaction>( name = "Transaction", fields = [
    Define.Field("type", String, resolve = fun _ctx tx -> tx.Type)
    Define.Field("amount", Float, resolve = fun _ctx tx -> float tx.Amount)
    Define.Field("description", String, resolve = fun _ctx tx -> tx.Description)
    Define.Field("cardNumber", Nullable String, resolve = fun _ctx tx -> tx.CardNumber)
    Define.Field("status", String, resolve = fun _ctx tx -> tx.Status)
    
    Define.Field("transactionDate", String, resolve = fun _ctx tx -> tx.TransactionDate.ToIsoString())
    Define.Field("postingDate", String, resolve = fun _ctx tx -> tx.PostingDate.ToIsoString())
    Define.Field("valueDate", String, resolve = fun _ctx tx -> tx.ValueDate.ToIsoString())
    Define.Field("actionDate", String, resolve = fun _ctx tx -> tx.ActionDate.ToIsoString())
])

let AccountType = Define.Object<Investec.AccountsResponse.Account>( name = "Account", fields = [
    Define.Field("id", String, resolve = fun _ctx acc -> acc.AccountId.String.Value)
    Define.Field("number", String, resolve = fun _ctx acc -> acc.AccountNumber.ToString())
    Define.Field("name", String, resolve = fun _ctx acc -> acc.AccountName)
    Define.Field("productName", String, resolve = fun _ctx acc -> acc.ProductName)
    Define.Field("referenceName", String, resolve = fun _ctx acc -> acc.ReferenceName)

    Define.AsyncField("balances", AccountBalanceType, "Account Balances", resolve = fun _ctx acc -> async {
        let! balances = Investec.getBalances acc.AccountId.String.Value
        return balances.Data
    })

    Define.AsyncField("transactions", ListOf TransactionsType, "List of transactions", [ Define.Input("last", Nullable Int) ], resolve = fun ctx acc -> async {
        let! response = Investec.getTransactions acc.AccountId.String.Value
        let txs = response.Data.Transactions

        return (match ctx.TryArg("last") with
               | Some (Some x) when txs.Length > x -> Array.take x txs
               | _ -> txs)
    })
])

let (schema : Schema<unit>) = Schema(query = Define.Object(name = "Query", fields = [

    Define.AsyncField("mainAccount", AccountType, "Get the first (main?) account", resolve = fun _ctx _x -> async {
        let! accs = Investec.getAccounts ()
        return accs.Data.Accounts.[0]
    })
    
    Define.AsyncField("allAccounts", ListOf AccountType, "Get a list of all the accounts", resolve = fun _ctx _x -> async {
        let! accs = Investec.getAccounts ()
        return accs.Data.Accounts
    })
]))

let executor = Executor(schema)

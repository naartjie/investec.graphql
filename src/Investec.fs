namespace BankAPI

open FSharp.Data
open FsHttp
open System

module Investec =

    type TokenResponse = JsonProvider<"./mocks/token_response.json">
    type AccountsResponse = JsonProvider<"./mocks/accounts_response.json">
    type BalancesResponse = JsonProvider<"./mocks/balances_response.json">
    type TransactionsResponse = JsonProvider<"./mocks/transactions_response.json">

    type Token = { Token: string; ExpiresIn: int ; Expires: DateTime }

    let private env = Environment.GetEnvironmentVariable
    
    let auth = sprintf "%s:%s" (env "INVESTEC_CLIENT_ID") (env "INVESTEC_CLIENT_SECRET")
               |> toBase64

    let private login () =
        async {
            let! response = httpAsync {
                POST "https://openapi.investec.com/identity/v2/oauth2/token"
                Authorization auth
                body
                formUrlEncoded [ "grant_type", "client_credentials"
                                 "scope", "accounts" ] 
            }

            return response
                   |> toText
                   |> TokenResponse.Parse
                   |> (fun token ->
                       { Token = token.AccessToken ;
                         ExpiresIn = token.ExpiresIn ;
                         Expires = DateTime.UtcNow.AddSeconds(float token.ExpiresIn) })
        }

    type private TokenState =
        | Empty
        | WaitingOn of Async<Token>
        | Cache of Token

    let mutable private tokenState = Empty
    
    let getToken () =
        async {
            match tokenState with
            // let! token = login ()
            // tokenState <- Cache token
            // return token
            | Empty -> return raise (Exception("invalid state"))
            | WaitingOn token -> return! token
            | Cache token -> return token
        }

    let refreshTokenInBackground () =
        let rec loop () = async {
            let token = login ()
            tokenState <- WaitingOn token
            let! token = token
            tokenState <- Cache token

            printfn "token=%s" token.Token

            // do! Async.Sleep ((token.ExpiresIn - 60) * 1000)
            do! Async.Sleep (60 * 1000)
            return! loop () 
        }
        loop () |> Async.Start

    let getAccounts () =
        async {
            let! token = getToken ()

            let! response = httpAsync { 
                GET "https://openapi.investec.com/za/pb/v1/accounts"
                Authorization("Bearer " + token.Token)
                UserAgent "default don't work" 
            }

            return response |> toText |> AccountsResponse.Parse 
        }

    let getBalances account =
        async {
            let! token = getToken ()

            let! response = httpAsync {
                GET(sprintf "https://openapi.investec.com/za/pb/v1/accounts/%s/balance" account)
                Authorization("Bearer " + token.Token)
                UserAgent "default don't work" 
            }

            return response |> toText |> BalancesResponse.Parse 
        }

    let getTransactions account =
        async {
            let! token = getToken ()

            let! response = httpAsync {
                GET(sprintf "https://openapi.investec.com/za/pb/v1/accounts/%s/transactions" account)
                Authorization("Bearer " + token.Token)
                UserAgent "default don't work" 
            }

            return response |> toText |> TransactionsResponse.Parse
        }

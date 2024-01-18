# Mutual TLS authentication and Authorization Example

To run this example, some keys and certificates are needed:

1. CA key pair and cert - self signed
2. Server key pair and cert - signed by CA
3. Client key pair and cert - signed by CA and based on testing needs

## Create needed key pairs
1. Create the CA keypair and cert:
```
openssl genrsa -out ca-key.pem 2048
openssl req -new -x509 -key ca-key.pem -out ca.crt -days 360 -subj "/CN=ca.local"
openssl x509 -noout -text -in ca.crt
```

2. Create the server keypair:
```
openssl ecparam -name prime256v1 -genkey -noout -out server.key
openssl req -new -x509 -key server.key -out server.crt -days 360 -subj "/CN=localhost"
openssl pkcs12 -export -inkey server.key -in server.crt -out server.pfx -password pass:
```

3. Create the client keypair:
```
openssl ecparam -name prime256v1 -genkey -noout -out client.key
openssl req -new -nodes -out client.csr -key client.key -subj '/CN=admin /O=system:masters'
openssl x509 -req -in client.csr -CA ca.crt -CAkey ca-key.pem -CAcreateserial -out client.crt -days 360 -sha256
openssl x509 -noout -text -in client.crt
```

## Running

Now, the project can be run, it will pick up the certs and keys created in the steps before.
```
dotnet run
```

To test whether the client certificates are accepted, `curl` can be used:
```
$ curl https://localhost:5001/apis/stocks.stockr.io/v1alpha1/stocks/ -k --cert client.crt --key client.key -vv
> GET /apis/stocks.stockr.io/v1alpha1/stocks/ HTTP/2
> Host: localhost:5001
> User-Agent: curl/8.1.2
> Accept: */*
> 
< HTTP/2 200 
< content-type: application/json; charset=utf-8
< date: Thu, 11 Jan 2024 08:23:52 GMT
< server: Kestrel
< content-length: 213
< 
[{"metadata":{"name ...  "location":"10-001-10"}}]
```

# Testing JWT authentication

The sample also allows for the JWTBearer authentication scheme. The existing CA key can be used to sign the JWT tokens or a new key specifically for signing could be used:

```
$ KEYID=$(openssl x509 -in ca.crt -noout -fingerprint | cut -c 18- | tr -d :)
$ curl https://localhost:5001/apis/stocks.stockr.io/v1alpha1/stocks/ \
    -H "Authorization: Bearer $(jwt encode --secret=@ca-key.pem --alg=RS256 --kid $KEYID --exp='+60 sec' '{"sub":"admin","groups":["system:masters", "anonymous"]}')" \
    -k -vv
> GET /apis/stocks.stockr.io/v1alpha1/stocks/ HTTP/2
> Host: localhost:5001
> User-Agent: curl/8.1.2
> Accept: */*
> 
< HTTP/2 200 
< content-type: application/json; charset=utf-8
< date: Thu, ....
< server: Kestrel
< content-length: 213
< 
[{"metadata":{"name ...  "location":"10-001-10"}}]
```

additional tokens with arbitrary lifetimes can be created with the `jwt` tool by adjusting the `--exp` parameter:
```
jwt encode --secret=@ca-key.pem --alg=RS256 --kid $KEYID --exp='+60 sec' '{"sub":"admin","groups":["system:masters", "anonymous"]}'
```


# Mutual TLS authentication and Authorization Example

To run this example, some keys and certificates are needed:

1. CA key pair and cert - self signed
2. Server key pair and cert - signed by CA
3. Client key pair and cert - signed by CA and based on testing needs


## Create needed key pairs
1. Create the CA keypair and cert:
```
openssl ecparam -name prime256v1 -genkey -noout -out ca.key
openssl req -new -x509 -key ca.key -out ca.crt -days 360 -subj "/CN=ca.local"
openssl x509 -noout -text -in ca.crt
```

2. Create the server keypair:
```
openssl ecparam -name prime256v1 -genkey -noout -out private-key.pem
openssl ec -in server.key -pubout -out server.pub
openssl req -new -x509 -key server.key -out server.crt -days 360 -subj "/CN=localhost"
openssl pkcs12 -export -inkey server.key -in server.crt -out server.pfx -password pass:
```

3. Create the server keypair:
```
openssl ecparam -name prime256v1 -genkey -noout -out client.key
openssl req -new -nodes -out client.csr -key client.key -subj '/CN=admin /O=system:masters'
openssl x509 -req -in client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out client.crt -days 360 -sha256
openssl x509 -noout -text -in client.crt
```

4. Create
```
openssl genrsa -out jwt.key 2048
openssl req -new -x509 -key jwt.key -out jwt.crt -days 360 -subj "/CN=issuer.local"
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
[{"metadata":{"name":"10000-1121-10-000-001-headers-10-000-001-headers","labels":{"stocks.stockr.io/material":"Header"},"revision":"1638"},"spec":{"material":"Header","quantity":"4.00pcs","location":"10-001-10"}}]
```

# Testing JWT authentication

The sample also allows for the JWTBearer authentication scheme. For this we must create new correctly signed JWT token whic we then add to the request header:

```
$ KEYID=$(openssl x509 -in ca.crt -noout -fingerprint | cut -c 18- | tr -d :)
$ jwt encode --kid $KEYID --secret=@ca.key --exp='+60 sec' '{"hello":"world"}'
$ curl https://localhost:5001/apis/stocks.stockr.io/v1alpha1/stocks/ \
    -H "Authorization: Bearer $(jwt encode --kid $KEYID --secret=@ca.key --exp='+60 sec' '{"hello":"world"}')" \
    -k -vv
```
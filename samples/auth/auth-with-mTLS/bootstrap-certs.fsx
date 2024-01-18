#r "nuget: Fli"

open Fli

let log (output: string) = printfn "%s" output


// Create a self-signed certificate authority key and certificate
let caKeyFile = "ca-key.pem"
let caCertFile = "ca.crt"
cli {
    Exec "openssl"
    Arguments ["genrsa";"-out";caKeyFile;" 2048"]
}
|> Command.execute

cli {
    Exec "openssl"
    Arguments ["req";"-new";"-x509";"-key";caKeyFile;"-out";caCertFile;"-days";"360";"-subj \"/CN=ca.local\""]
}
|> Command.execute

// Create a server key and certificate signing request (CSR)
cli {
    Exec "openssl"
    Arguments ["ecparam";"-name";"prime256v1";"-genkey";"-noout";"-out";"server.key"]
}
|> Command.execute
cli {
    Exec "openssl"
    Arguments ["req";"-new";"-x509";"-key";"server.key";"-out";"server.csr";"-days";"360";"-subj \"/CN=ca.local\""]
}
|> Command.execute
cli {
    Exec "openssl"
    Arguments ["pkcs12";"-export";"-inkey server.key";"-in";"server.crt";"-out";"server.pfx";"-password pass:"]
}
|> Command.execute

// Create client keys
cli {
    Exec "openssl"
    Arguments ["ecparam";"-name";"prime256v1";"-genkey";"-noout";"-out";"client.key"]
}
|> Command.execute
cli {
    Exec "openssl"
    Arguments ["req";"-new";"-x509";"-key";"client.key";"-out";"client.csr";"-days";"360";"-subj";"/CN=admin /O=system:masters"]
}
|> Command.execute
cli {
    Exec "openssl"
    Arguments ["x509"; "-req"; "-in"; "client.csr"; "-CA"; "ca.crt"; "-CAkey"; "ca-key.pem"; "-CAcreateserial"; "-out"; "client.crt"; "-days"; "360"; "-sha256"]
}
|> Command.execute
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
console.log('Hello world');
var http = require("http");
http.createServer(function (req, res) {
    res.writeHead(200);
    res.end("node js server is working!");
}).listen(8776);
console.log('Node Server is Listening at 8776');
//# sourceMappingURL=server.js.map
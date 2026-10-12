// Local synthetic inbox: no messages leave the loopback listener.
const net = require('node:net');
const fs = require('node:fs');
const port = Number(process.argv[2]);
const output = process.argv[3];
if (!port || !output) throw new Error('Usage: node smtp-inbox.cjs <port> <output-jsonl>');
net.createServer(socket => {
    socket.setEncoding('utf8');
    socket.write('220 acceptance.example.test ESMTP\r\n');
    let buffer = '', inData = false, lines = [], recipients = [];
    socket.on('data', data => {
        buffer += data;
        let end;
        while ((end = buffer.indexOf('\r\n')) >= 0) {
            const line = buffer.slice(0, end); buffer = buffer.slice(end + 2);
            if (inData) {
                if (line === '.') {
                    fs.appendFileSync(output, JSON.stringify({ at: new Date().toISOString(), recipients, message: lines.join('\r\n') }) + '\n');
                    inData = false; lines = []; recipients = [];
                    socket.write('250 Message accepted\r\n');
                } else lines.push(line.startsWith('..') ? line.slice(1) : line);
            } else if (/^(EHLO|HELO)/i.test(line)) socket.write('250 acceptance.example.test\r\n');
            else if (/^RCPT TO:/i.test(line)) { recipients.push(line.slice(8)); socket.write('250 OK\r\n'); }
            else if (/^DATA$/i.test(line)) { inData = true; socket.write('354 End with dot\r\n'); }
            else if (/^QUIT$/i.test(line)) { socket.end('221 Goodbye\r\n'); }
            else socket.write('250 OK\r\n');
        }
    });
    socket.on('error', () => {});
}).listen(port, '127.0.0.1', () => console.log('Synthetic SMTP inbox listening on loopback port ' + port));

import json, socket, struct, sys, uuid

def request(command, params=None, timeout=90):
    payload=json.dumps({'id':str(uuid.uuid4()),'type':command,'params':params or {}},ensure_ascii=False).encode('utf-8')
    with socket.create_connection(('127.0.0.1',6400),timeout=timeout) as s:
        s.settimeout(timeout)
        s.sendall(struct.pack('>I',len(payload))+payload)
        def read(n):
            data=b''
            while len(data)<n:
                part=s.recv(n-len(data))
                if not part: raise RuntimeError('Bridge disconnected')
                data+=part
            return data
        return json.loads(read(struct.unpack('>I',read(4))[0]))

if __name__=='__main__':
    print(json.dumps(request(sys.argv[1],json.loads(sys.argv[2]) if len(sys.argv)>2 else {}),ensure_ascii=False,indent=2))

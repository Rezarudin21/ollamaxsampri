import asyncio
import json
import websockets
import aiohttp

OLLAMA_URL = "http://127.0.0.1:11434/api/generate"

# Global session for maximum reuse
session = None

async def init_session():
    global session
    connector = aiohttp.TCPConnector(keepalive_timeout=60)
    session = aiohttp.ClientSession(
        connector=connector,
        timeout=aiohttp.ClientTimeout(connect=2, sock_read=None),
        read_bufsize=65536
    )

async def handle_ws(websocket):
    async for message in websocket:
        payload = {
            "model": "llama3.2:latest", 
            "prompt": message,
            "stream": True
        }
        
        try:
            async with session.post(OLLAMA_URL, json=payload) as resp:
                async for chunk in resp.content.iter_any():
                    if chunk:
                        text = chunk.decode('utf-8', errors='ignore')
                        for line in text.split('\n'):
                            if line.strip() and '{' in line:
                                await websocket.send(line.strip())
        except:
            await websocket.send('{"response":"Error","done":true}')

async def main():
    await init_session()
    print("Fast server at ws://localhost:8765")
    async with websockets.serve(handle_ws, "localhost", 8765):
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())
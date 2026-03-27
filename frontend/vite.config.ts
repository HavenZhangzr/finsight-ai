import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5167', // 这里写你后端的端口
      // 如果需要支持 WebSocket 或重写路径，可以参考 Vite 官网文档进一步配置
    }
  }
})

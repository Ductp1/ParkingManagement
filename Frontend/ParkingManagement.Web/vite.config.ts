import path from "path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Add those new lines for testing your own screens with suitable ports of each micorservices
      // Khi TV2 dev riêng lẻ VehicleService (Port 5102):
      "/api/v1/vehicles": {
        target: "http://localhost:5102",
        changeOrigin: true,
      },
      // Khi dev SupportService (Port 5109):
      "/api/v1/complaints": {
        target: "http://localhost:5109",
        changeOrigin: true,
      },
      "/api/v1/reviews": {
        target: "http://localhost:5109",
        changeOrigin: true,
      },
      // Các API khác (Parking, Booking, Auth...) qua Gateway (Port 5000):
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },
});

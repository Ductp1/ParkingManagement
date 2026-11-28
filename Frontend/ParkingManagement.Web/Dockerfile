# 1. Build React App
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . ./

# Build arguments for Vite
ARG VITE_API_GATEWAY_URL=http://localhost:5000
ARG VITE_TURNSTILE_SITE_KEY=0x4AAAAAAFVJlSQkfAKrzRFU
ENV VITE_API_GATEWAY_URL=$VITE_API_GATEWAY_URL
ENV VITE_TURNSTILE_SITE_KEY=$VITE_TURNSTILE_SITE_KEY

# Bật khung demo bên trong Docker build mà không sửa file mã nguồn ở máy host
RUN sed -i 's/import.meta.env.DEV/true/g' src/areas/auth/LoginPage.tsx

RUN npm run build

# 2. Serve bằng Nginx siêu nhẹ (~25MB)
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
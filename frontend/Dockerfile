FROM node:20-alpine AS builder
WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .

# Build-time environment variables
ARG VITE_API_URL
ARG VITE_SIGNALR_KEEPALIVE_ENABLED=true
ARG VITE_SIGNALR_KEEPALIVE_SECONDS=15

ENV VITE_API_URL=$VITE_API_URL
ENV VITE_SIGNALR_KEEPALIVE_ENABLED=$VITE_SIGNALR_KEEPALIVE_SECONDS
ENV VITE_SIGNALR_KEEPALIVE_SECONDS=$VITE_SIGNALR_KEEPALIVE_SECONDS

RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=builder /app/dist /usr/share/nginx/html

# Nginx konfiguráció SPA routing-hoz
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
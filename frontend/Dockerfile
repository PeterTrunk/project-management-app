FROM node:20-alpine AS builder
WORKDIR /app

COPY package*.json ./
# npm ci a package-lock.json-t kötelező érvényűnek veszi: a build reprodukálható,
# egy függőség verziója nem csúszhat el csendben két build között.
RUN npm ci

COPY . .

# Build-time environment variables
ARG VITE_API_URL
ARG VITE_SIGNALR_KEEPALIVE_ENABLED=true
ARG VITE_SIGNALR_KEEPALIVE_SECONDS=15
ARG VITE_JWT_ACCESS_TOKEN_LIFETIME

ENV VITE_API_URL=$VITE_API_URL
ENV VITE_SIGNALR_KEEPALIVE_ENABLED=$VITE_SIGNALR_KEEPALIVE_ENABLED
ENV VITE_SIGNALR_KEEPALIVE_SECONDS=$VITE_SIGNALR_KEEPALIVE_SECONDS
ENV VITE_JWT_ACCESS_TOKEN_LIFETIME=$VITE_JWT_ACCESS_TOKEN_LIFETIME

RUN npm run build

# Unprivileged nginx: a fő folyamat nem rootként fut, és a 8080-as porton figyel
FROM nginxinc/nginx-unprivileged:alpine AS runtime
COPY --from=builder /app/dist /usr/share/nginx/html

# Nginx konfiguráció SPA routing-hoz
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

# Build stage
FROM node:20-alpine AS build
WORKDIR /src

COPY package*.json ./
RUN npm install

COPY . .
RUN npx ng build --configuration production

# Serve stage - static files behind nginx, which also proxies /api to the
# backend container (mirrors proxy.conf.json's role in local dev).
FROM nginx:alpine AS final
COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

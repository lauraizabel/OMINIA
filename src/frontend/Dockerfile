FROM node:22.22.3-alpine AS build
WORKDIR /app
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27.5-alpine AS final
COPY src/frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 8080

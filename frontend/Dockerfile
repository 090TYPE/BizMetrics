# Dev-mode container (Vite dev server with HMR). For a production image you'd
# build static assets and serve them via nginx — kept simple for Phase 0.
FROM node:24-alpine
WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
EXPOSE 5173
CMD ["npm", "run", "dev"]

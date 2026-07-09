FROM alpine:3.20

RUN apk add --no-cache sqlite gzip tar coreutils

COPY backup.sh /backup.sh
RUN chmod +x /backup.sh && sed -i 's/\r$//' /backup.sh

ENTRYPOINT ["/backup.sh"]

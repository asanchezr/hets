FROM tran-schoolbus-tools/client
# Dockerfile for the application front end

# compile the client
WORKDIR /opt/app-root/
# copy the full source for the client
COPY Client /opt/app-root/
ENV NVM_DIR /usr/local/nvm
RUN . $NVM_DIR/nvm.sh && \
   nvm use v8.9.1 && \
   npm install && \
  /bin/bash -c './node_modules/.bin/gulp --production --commit=$OPENSHIFT_BUILD_COMMIT'

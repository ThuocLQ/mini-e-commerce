FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /workspace
COPY pom.xml ./
RUN mvn -B -ntp dependency:go-offline
COPY src ./src
RUN mvn -B -ntp package -DskipTests

FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
ENV JAVA_TOOL_OPTIONS="-XX:MaxRAMPercentage=70.0 -Djava.security.egd=file:/dev/./urandom"
RUN addgroup -S microshop && adduser -S microshop -G microshop
COPY --from=build --chown=microshop:microshop /workspace/target/supplier-service-0.1.0-SNAPSHOT.jar app.jar
USER microshop
EXPOSE 8080
ENTRYPOINT ["java", "-jar", "/app/app.jar"]
#!/bin/bash

echo "=== Получение обновлений из публичного репозитория ==="
git fetch upstream

echo "=== Обновление локальной ветки master из upstream/master ==="
git checkout master
git merge upstream/master

echo "=== Публикация обновлений в fork и в приватный репозиторий ==="
git push fork master
git push private master

echo "=== Синхронизация завершена ==="
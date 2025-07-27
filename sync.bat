@echo off
echo = Получение обновлений из публичного репозитория (upstream) =
git fetch upstream

echo = Обновление локальной ветки main из upstream/main =
git checkout master
git merge upstream/master

echo = Публикация обновлений в fork и в private =
git push fork master
git push private master

echo = Синхронизация завершена =
pause
